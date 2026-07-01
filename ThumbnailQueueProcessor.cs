using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
  /// <summary>
  /// サムネイル作成キューの監視・進捗表示・並列実行を担当する。
  /// 常時 N ワーカーが 1 件ずつ dequeue して処理する（Tier B-4 パイプライン）。
  /// </summary>
  public sealed class ThumbnailQueueProcessor
  {
    private const int ProgressReportIntervalMs = 150;
    private const int ProgressShowDelayMs = 700;

    private static Action s_cancelPendingProgressShow;
    private static Action s_dismissActiveProgress;

    /// <summary>
    /// タブ切替時に UI スレッドから即座に進捗ポップアップを閉じる。
    /// </summary>
    public static void RequestDismissProgress()
    {
      ThumbnailProgressRegistry.DismissAll();
      s_cancelPendingProgressShow?.Invoke();
      s_dismissActiveProgress?.Invoke();
    }

    public async Task RunAsync(
      ConcurrentQueue<QueueObj> queueThumb,
      Func<QueueObj, CancellationToken, Task> createThumbAsync,
      ThumbnailJobCoordinator jobCoordinator,
      object queueSync,
      int maxParallelism = 4,
      Func<int> maxParallelismResolver = null,
      int pollIntervalMs = 100,
      Action<string> log = null,
      CancellationToken cts = default,
      Func<CancellationToken> batchCancellationToken = null)
    {
      Dispatcher uiDispatcher = Application.Current?.Dispatcher;
      int safePollIntervalMs = pollIntervalMs < 50 ? 50 : pollIntervalMs;
      object progressLock = new();
      ThumbnailProgressSession activeProgress = null;
      int activeProgressJobId = -1;
      DateTime lastReportUtc = DateTime.MinValue;
      CancellationTokenSource progressShowCts = null;
      int lastJobSwitchToken = jobCoordinator.JobSwitchToken;

      void CancelPendingProgressShow()
      {
        if (progressShowCts == null)
        {
          return;
        }

        progressShowCts.Cancel();
        progressShowCts.Dispose();
        progressShowCts = null;
      }

      void ReplaceProgressShowCts(CancellationTokenSource next)
      {
        CancelPendingProgressShow();
        if (next != null)
        {
          progressShowCts = next;
        }
      }

      void DismissActiveProgressNow()
      {
        CancelPendingProgressShow();

        ThumbnailProgressSession progressToClose;
        lock (progressLock)
        {
          progressToClose = activeProgress;
          activeProgress = null;
          activeProgressJobId = -1;
          lastReportUtc = DateTime.MinValue;
        }

        if (progressToClose == null)
        {
          return;
        }

        if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
        {
          progressToClose.Dispose();
          return;
        }

        if (uiDispatcher.CheckAccess())
        {
          progressToClose.Dispose();
          return;
        }

        uiDispatcher.Invoke(() => progressToClose.Dispose(), DispatcherPriority.Send);
      }

      s_cancelPendingProgressShow = CancelPendingProgressShow;
      s_dismissActiveProgress = DismissActiveProgressNow;

      int safeMaxParallelism = ResolveMaxParallelism(maxParallelism, maxParallelismResolver);

      async Task ProcessQueueItemAsync(QueueObj item, CancellationToken itemToken)
      {
        if (!jobCoordinator.ShouldProcess(item))
        {
          jobCoordinator.TrySkipItem(item);
          return;
        }

        jobCoordinator.MarkInFlight(item);

        await MaybeShowProgressAfterMarkInFlightAsync(
          uiDispatcher,
          jobCoordinator,
          progressLock,
          () => activeProgress,
          session => activeProgress = session,
          () => activeProgressJobId,
          jobId => activeProgressJobId = jobId,
          utc => lastReportUtc = utc,
          item).ConfigureAwait(false);

        try
        {
          await createThumbAsync(item, itemToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
          Debug.WriteLine(
            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] item failed: {ex.Message}");
        }
        finally
        {
          ThumbnailJobCoordinator.Snapshot snapshot = jobCoordinator.TryComplete(item);

          ThumbnailProgressSession progressToReport;
          int progressJobId;
          lock (progressLock)
          {
            progressToReport = activeProgress;
            progressJobId = activeProgressJobId;
          }

          if (progressToReport != null && item.JobId == progressJobId)
          {
            if (snapshot.Abandoned)
            {
              if (snapshot.IsComplete)
              {
                CancelPendingProgressShow();
                await DisposeProgressAsync(uiDispatcher, progressToReport, progressLock).ConfigureAwait(false);
                lock (progressLock)
                {
                  if (activeProgress == progressToReport)
                  {
                    activeProgress = null;
                    activeProgressJobId = -1;
                    lastReportUtc = DateTime.MinValue;
                  }
                }
              }
            }
            else
            {
              bool shouldReport;
              lock (progressLock)
              {
                DateTime now = DateTime.UtcNow;
                shouldReport = snapshot.IsComplete
                  || (now - lastReportUtc).TotalMilliseconds >= ProgressReportIntervalMs;
                if (shouldReport)
                {
                  lastReportUtc = now;
                }
              }

              if (shouldReport)
              {
                try
                {
                  await ReportProgressOnUiAsync(
                    uiDispatcher,
                    progressToReport,
                    jobCoordinator,
                    progressJobId,
                    snapshot.Completed,
                    snapshot.Total,
                    GetThumbProgressDetail(item)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                  Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-progress] {ex.Message}");
                }
              }

              if (snapshot.IsComplete)
              {
                CancelPendingProgressShow();
                await DisposeProgressAsync(uiDispatcher, progressToReport, progressLock).ConfigureAwait(false);
                lock (progressLock)
                {
                  if (activeProgress == progressToReport)
                  {
                    activeProgress = null;
                    activeProgressJobId = -1;
                    lastReportUtc = DateTime.MinValue;
                  }
                }
              }
            }
          }
          else if (progressToReport != null
            && item.JobId != progressJobId
            && snapshot.IsComplete
            && snapshot.Abandoned)
          {
            CancelPendingProgressShow();
            await DisposeProgressAsync(uiDispatcher, progressToReport, progressLock).ConfigureAwait(false);
            lock (progressLock)
            {
              if (activeProgress == progressToReport)
              {
                activeProgress = null;
                activeProgressJobId = -1;
                lastReportUtc = DateTime.MinValue;
              }
            }
          }
        }
      }

      async Task TryCloseProgressIfJobDrainedAsync()
      {
        ThumbnailProgressSession progressToClose;
        int closingJobId;
        lock (progressLock)
        {
          progressToClose = activeProgress;
          closingJobId = activeProgressJobId;
        }

        if (progressToClose == null || closingJobId <= 0)
        {
          return;
        }

        ThumbnailJobCoordinator.Snapshot afterItem = jobCoordinator.GetSnapshot(closingJobId);
        if (!afterItem.IsComplete
          || afterItem.JobId != closingJobId
          || afterItem.Abandoned
          || HasQueuedWorkForJob(queueThumb, queueSync, closingJobId))
        {
          return;
        }

        CancelPendingProgressShow();
        await DisposeProgressAsync(uiDispatcher, progressToClose, progressLock).ConfigureAwait(false);
        lock (progressLock)
        {
          if (activeProgress == progressToClose)
          {
            activeProgress = null;
            activeProgressJobId = -1;
            lastReportUtc = DateTime.MinValue;
          }
        }
      }

      async Task WorkerLoopAsync()
      {
        while (!cts.IsCancellationRequested)
        {
          try
          {
            QueueObj item = TryDequeueOne(queueThumb, queueSync);
            if (item == null)
            {
              await Task.Delay(safePollIntervalMs, cts).ConfigureAwait(false);
              continue;
            }

            await SwitchProgressToCurrentJobAsync(
              uiDispatcher,
              jobCoordinator,
              progressLock,
              () => activeProgress,
              session => activeProgress = session,
              () => activeProgressJobId,
              jobId => activeProgressJobId = jobId,
              utc => lastReportUtc = utc,
              ReplaceProgressShowCts).ConfigureAwait(false);

            CancellationToken batchToken = batchCancellationToken?.Invoke() ?? cts;
            CancellationToken jobToken = jobCoordinator.GetJobCancellationToken(item.JobId);
            using CancellationTokenSource linkedCts =
              CancellationTokenSource.CreateLinkedTokenSource(cts, batchToken, jobToken);

            try
            {
              await ProcessQueueItemAsync(item, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cts.IsCancellationRequested)
            {
              CancelPendingProgressShow();
              Debug.WriteLine(
                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : サムネイル作成バッチをキャンセルしました。");
            }

            await TryCloseProgressIfJobDrainedAsync().ConfigureAwait(false);

            await SwitchProgressToCurrentJobAsync(
              uiDispatcher,
              jobCoordinator,
              progressLock,
              () => activeProgress,
              session => activeProgress = session,
              () => activeProgressJobId,
              jobId => activeProgressJobId = jobId,
              utc => lastReportUtc = utc,
              ReplaceProgressShowCts).ConfigureAwait(false);
          }
          catch (OperationCanceledException) when (cts.IsCancellationRequested)
          {
            return;
          }
          catch (Exception ex)
          {
            string itemMsg = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-worker] {ex.Message}";
            Debug.WriteLine(itemMsg);
            log?.Invoke(itemMsg);
          }
        }
      }

      Task[] workers = new Task[safeMaxParallelism];
      for (int i = 0; i < safeMaxParallelism; i++)
      {
        workers[i] = WorkerLoopAsync();
      }

      try
      {
        while (true)
        {
          try
          {
            await Task.Delay(safePollIntervalMs, cts).ConfigureAwait(false);

            int switchToken = jobCoordinator.JobSwitchToken;
            if (switchToken != lastJobSwitchToken)
            {
              lastJobSwitchToken = switchToken;
              await DismissActiveProgressSilentlyAsync(
                uiDispatcher,
                progressLock,
                () => activeProgress,
                session => activeProgress = session,
                () => activeProgressJobId,
                jobId => activeProgressJobId = jobId,
                utc => lastReportUtc = utc,
                ReplaceProgressShowCts).ConfigureAwait(false);
            }

            if (queueThumb.IsEmpty)
            {
              ThumbnailJobCoordinator.Snapshot idleSnapshot;
              int trackedJobId;
              lock (progressLock)
              {
                trackedJobId = activeProgressJobId;
              }

              idleSnapshot = trackedJobId > 0
                ? jobCoordinator.GetSnapshot(trackedJobId)
                : jobCoordinator.GetSnapshot();

              if (activeProgress != null
                && idleSnapshot.JobId == trackedJobId
                && (idleSnapshot.IsComplete || idleSnapshot.Abandoned))
              {
                CancelPendingProgressShow();
                await DisposeProgressAsync(uiDispatcher, activeProgress, progressLock).ConfigureAwait(false);
                activeProgress = null;
                activeProgressJobId = -1;
                lastReportUtc = DateTime.MinValue;
              }

              continue;
            }

            await TryCloseProgressIfJobDrainedAsync().ConfigureAwait(false);
          }
          catch (OperationCanceledException) when (cts.IsCancellationRequested)
          {
            throw;
          }
          catch (Exception ex)
          {
            string itemMsg = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-loop] {ex.Message}";
            Debug.WriteLine(itemMsg);
            log?.Invoke(itemMsg);
          }
        }
      }
      catch (OperationCanceledException)
      {
        CancelPendingProgressShow();
        string msg = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : サムネイルキュー監視をキャンセルしました。";
        Debug.WriteLine(msg);
        log?.Invoke(msg);
      }
      catch (Exception e)
      {
        string msg = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : {e.Message}";
        Debug.WriteLine(msg);
        log?.Invoke(msg);
      }
      finally
      {
        try
        {
          await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch
        {
          // ワーカー終了時の例外は握りつぶし、後始末を続行する。
        }

        s_cancelPendingProgressShow = null;
        s_dismissActiveProgress = null;
        CancelPendingProgressShow();
        ThumbnailProgressSession progressToDispose;
        lock (progressLock)
        {
          progressToDispose = activeProgress;
          activeProgress = null;
        }

        if (progressToDispose != null)
        {
          await DisposeProgressAsync(uiDispatcher, progressToDispose, progressLock).ConfigureAwait(false);
        }
      }
    }

    private static bool IsProgressShowAllowedNow(
      ThumbnailJobCoordinator jobCoordinator,
      int jobIdToShow,
      int jobSwitchToken)
    {
      if (jobCoordinator.JobSwitchToken != jobSwitchToken)
      {
        return false;
      }

      ThumbnailJobCoordinator.Snapshot snapshot = jobCoordinator.GetSnapshot(jobIdToShow);
      return snapshot.JobId == jobIdToShow
        && snapshot.Total > 0
        && snapshot.InFlight > 0
        && !snapshot.IsComplete
        && !snapshot.Abandoned
        && jobIdToShow == jobCoordinator.CurrentJobId;
    }

    private static async Task MaybeShowProgressAfterMarkInFlightAsync(
      Dispatcher uiDispatcher,
      ThumbnailJobCoordinator jobCoordinator,
      object progressLock,
      Func<ThumbnailProgressSession> getActiveProgress,
      Action<ThumbnailProgressSession> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Action<DateTime> setLastReportUtc,
      QueueObj item)
    {
      if (item == null || item.JobId == ThumbnailJobCoordinator.SilentJobId)
      {
        return;
      }

      int jobId = item.JobId;
      ThumbnailJobCoordinator.Snapshot snapshot = jobCoordinator.GetSnapshot(jobId);
      int jobSwitchToken = jobCoordinator.JobSwitchToken;
      int delayMs = snapshot.Total > 1 ? 0 : ProgressShowDelayMs;

      lock (progressLock)
      {
        if (getActiveProgress() != null || getActiveProgressJobId() != -1)
        {
          return;
        }
      }

      if (!IsProgressShowAllowedNow(jobCoordinator, jobId, jobSwitchToken))
      {
        return;
      }

      if (delayMs > 0)
      {
        await Task.Delay(delayMs).ConfigureAwait(false);
      }

      if (!IsProgressShowAllowedNow(jobCoordinator, jobId, jobSwitchToken))
      {
        return;
      }

      ThumbnailProgressSession session = await TryActivateProgressOnUiAsync(
        uiDispatcher,
        jobCoordinator,
        progressLock,
        getActiveProgress,
        setActiveProgress,
        getActiveProgressJobId,
        setActiveProgressJobId,
        setLastReportUtc,
        jobId,
        snapshot.PrimaryTabIndex,
        jobSwitchToken).ConfigureAwait(false);

      if (session == null)
      {
        return;
      }

      if (!IsProgressShowAllowedNow(jobCoordinator, jobId, jobSwitchToken))
      {
        await AbandonActivatedSessionAsync(
          uiDispatcher,
          progressLock,
          getActiveProgress,
          setActiveProgress,
          getActiveProgressJobId,
          setActiveProgressJobId,
          setLastReportUtc,
          session).ConfigureAwait(false);
        return;
      }

      snapshot = jobCoordinator.GetSnapshot(jobId);
      try
      {
        await ReportProgressOnUiAsync(
          uiDispatcher,
          session,
          jobCoordinator,
          jobId,
          snapshot.Completed,
          snapshot.Total,
          GetThumbProgressDetail(item)).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-progress] {ex.Message}");
      }
    }

    private static async Task AbandonActivatedSessionAsync(
      Dispatcher uiDispatcher,
      object progressLock,
      Func<ThumbnailProgressSession> getActiveProgress,
      Action<ThumbnailProgressSession> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Action<DateTime> setLastReportUtc,
      ThumbnailProgressSession session)
    {
      lock (progressLock)
      {
        if (getActiveProgress() == session)
        {
          setActiveProgress(null);
          setActiveProgressJobId(-1);
          setLastReportUtc(DateTime.MinValue);
        }
      }

      await DisposeProgressAsync(uiDispatcher, session, progressLock).ConfigureAwait(false);
    }

    private static Task<ThumbnailProgressSession> TryActivateProgressOnUiAsync(
      Dispatcher uiDispatcher,
      ThumbnailJobCoordinator jobCoordinator,
      object progressLock,
      Func<ThumbnailProgressSession> getActiveProgress,
      Action<ThumbnailProgressSession> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Action<DateTime> setLastReportUtc,
      int jobIdToShow,
      int primaryTabIndex,
      int jobSwitchToken)
    {
      ThumbnailProgressSession Activate()
      {
        if (!IsProgressShowAllowedNow(jobCoordinator, jobIdToShow, jobSwitchToken))
        {
          return null;
        }

        lock (progressLock)
        {
          if (getActiveProgress() != null || getActiveProgressJobId() != -1)
          {
            return null;
          }

          if (!IsProgressShowAllowedNow(jobCoordinator, jobIdToShow, jobSwitchToken))
          {
            return null;
          }

          ThumbnailProgressSession session = new(primaryTabIndex, jobSwitchToken);
          setActiveProgress(session);
          setActiveProgressJobId(jobIdToShow);
          setLastReportUtc(DateTime.MinValue);
          return session;
        }
      }

      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        return Task.FromResult(Activate());
      }

      if (uiDispatcher.CheckAccess())
      {
        return Task.FromResult(Activate());
      }

      return uiDispatcher.InvokeAsync(Activate, DispatcherPriority.Send).Task;
    }

    private static async Task SwitchProgressToCurrentJobAsync(
      Dispatcher uiDispatcher,
      ThumbnailJobCoordinator jobCoordinator,
      object progressLock,
      Func<ThumbnailProgressSession> getActiveProgress,
      Action<ThumbnailProgressSession> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Action<DateTime> setLastReportUtc,
      Action<CancellationTokenSource> assignProgressShowCts)
    {
      int currentJobId = jobCoordinator.CurrentJobId;
      ThumbnailProgressSession progressToClose;
      int trackedJobId;

      lock (progressLock)
      {
        progressToClose = getActiveProgress();
        trackedJobId = getActiveProgressJobId();
      }

      if (progressToClose == null || trackedJobId <= 0 || trackedJobId == currentJobId)
      {
        return;
      }

      await DismissActiveProgressSilentlyAsync(
        uiDispatcher,
        progressLock,
        getActiveProgress,
        setActiveProgress,
        getActiveProgressJobId,
        setActiveProgressJobId,
        setLastReportUtc,
        assignProgressShowCts).ConfigureAwait(false);
    }

    private static async Task DismissActiveProgressSilentlyAsync(
      Dispatcher uiDispatcher,
      object progressLock,
      Func<ThumbnailProgressSession> getActiveProgress,
      Action<ThumbnailProgressSession> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Action<DateTime> setLastReportUtc,
      Action<CancellationTokenSource> assignProgressShowCts)
    {
      assignProgressShowCts(null);

      ThumbnailProgressSession progressToClose;
      lock (progressLock)
      {
        progressToClose = getActiveProgress();
      }

      if (progressToClose == null)
      {
        return;
      }

      await DisposeProgressAsync(uiDispatcher, progressToClose, progressLock).ConfigureAwait(false);
      lock (progressLock)
      {
        if (getActiveProgress() == progressToClose)
        {
          setActiveProgress(null);
          setActiveProgressJobId(-1);
          setLastReportUtc(DateTime.MinValue);
        }
      }
    }

    private static bool HasQueuedWorkForJob(
      ConcurrentQueue<QueueObj> queueThumb,
      object queueSync,
      int jobId)
    {
      lock (queueSync)
      {
        foreach (QueueObj item in queueThumb)
        {
          if (item != null && item.JobId == jobId)
          {
            return true;
          }
        }
      }

      return false;
    }

    private static string GetThumbProgressDetail(QueueObj item)
    {
      if (item == null)
      {
        return string.Empty;
      }

      if (!string.IsNullOrWhiteSpace(item.LastThumbProgressDetail))
      {
        return item.LastThumbProgressDetail;
      }

      return item.MovieFullPath ?? string.Empty;
    }

    private static Task ReportProgressOnUiAsync(
      Dispatcher uiDispatcher,
      ThumbnailProgressSession session,
      ThumbnailJobCoordinator coordinator,
      int jobId,
      int completed,
      int total,
      string detail)
    {
      if (session == null || coordinator == null)
      {
        return Task.CompletedTask;
      }

      void Report()
      {
        session.TryReport(coordinator, jobId, completed, total, detail);
      }

      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        Report();
        return Task.CompletedTask;
      }

      if (uiDispatcher.CheckAccess())
      {
        Report();
        return Task.CompletedTask;
      }

      return uiDispatcher.InvokeAsync(Report, DispatcherPriority.Send).Task;
    }

    private static QueueObj TryDequeueOne(ConcurrentQueue<QueueObj> queueThumb, object queueSync)
    {
      lock (queueSync)
      {
        while (queueThumb.TryDequeue(out QueueObj queueObj))
        {
          if (queueObj != null)
          {
            return queueObj;
          }
        }
      }

      return null;
    }

    private static Task DisposeProgressAsync(
      Dispatcher uiDispatcher,
      ThumbnailProgressSession session,
      object progressLock)
    {
      if (session == null)
      {
        return Task.CompletedTask;
      }

      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        lock (progressLock)
        {
          session.Dispose();
        }

        return Task.CompletedTask;
      }

      if (uiDispatcher.CheckAccess())
      {
        lock (progressLock)
        {
          session.Dispose();
        }

        return Task.CompletedTask;
      }

      return uiDispatcher.InvokeAsync(() =>
      {
        lock (progressLock)
        {
          session.Dispose();
        }
      }, DispatcherPriority.Background).Task;
    }

    private static int ResolveMaxParallelism(int maxParallelism, Func<int> maxParallelismResolver)
    {
      int resolved = maxParallelism;
      if (maxParallelismResolver != null)
      {
        try
        {
          resolved = maxParallelismResolver();
        }
        catch
        {
          resolved = maxParallelism;
        }
      }

      return ClampThumbnailParallelism(resolved);
    }

    public static int ClampThumbnailParallelism(int parallelism)
    {
      if (parallelism < 1)
      {
        return 1;
      }

      int upperBound = Math.Max(Environment.ProcessorCount * 2, 1);
      if (parallelism > upperBound)
      {
        return upperBound;
      }

      return parallelism;
    }
  }
}
