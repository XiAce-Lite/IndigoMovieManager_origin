using Notification.Wpf;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace IndigoMovieManager
{
  /// <summary>
  /// サムネイル作成キューの監視・進捗表示・並列実行を担当する。
  /// </summary>
  public sealed class ThumbnailQueueProcessor
  {
    private const int ProgressReportIntervalMs = 150;
    private const int ProgressShowDelayMs = 700;

    public async Task RunAsync(
      ConcurrentQueue<QueueObj> queueThumb,
      Func<QueueObj, CancellationToken, Task> createThumbAsync,
      ThumbnailJobCoordinator jobCoordinator,
      object queueSync,
      int maxParallelism = 4,
      Func<int> maxParallelismResolver = null,
      int pollIntervalMs = 100,
      Action<string> log = null,
      CancellationToken cts = default)
    {
      Dispatcher uiDispatcher = Application.Current?.Dispatcher;
      NotificationManager notificationManager = await CreateNotificationManagerAsync(uiDispatcher)
        .ConfigureAwait(false);
      int safePollIntervalMs = pollIntervalMs < 50 ? 50 : pollIntervalMs;
      object progressLock = new();
      IDisposable activeProgress = null;
      int activeProgressJobId = -1;
      DateTime lastReportUtc = DateTime.MinValue;
      CancellationTokenSource progressShowCts = null;

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

      try
      {
        while (true)
        {
          await Task.Delay(safePollIntervalMs, cts).ConfigureAwait(false);
          if (queueThumb.IsEmpty)
          {
            ThumbnailJobCoordinator.Snapshot idleSnapshot = jobCoordinator.GetSnapshot();
            if (activeProgress != null && idleSnapshot.IsComplete)
            {
              CancelPendingProgressShow();
              await DisposeProgressAsync(uiDispatcher, activeProgress, progressLock).ConfigureAwait(false);
              activeProgress = null;
              activeProgressJobId = -1;
              lastReportUtc = DateTime.MinValue;
            }

            continue;
          }

          List<QueueObj> batch = DequeueBatch(queueThumb, queueSync);
          if (batch.Count < 1)
          {
            continue;
          }

          ThumbnailJobCoordinator.Snapshot batchSnapshot = jobCoordinator.GetSnapshot();
          if (batchSnapshot.Total > 0
            && activeProgress == null
            && batch.Any(item => item != null && item.JobId == batchSnapshot.JobId))
          {
            CancelPendingProgressShow();
            progressShowCts = CancellationTokenSource.CreateLinkedTokenSource(cts);
            CancellationToken showToken = progressShowCts.Token;
            int jobIdToShow = batchSnapshot.JobId;
            int primaryTabIndex = batchSnapshot.PrimaryTabIndex;

            _ = ShowProgressAfterDelayAsync(
              uiDispatcher,
              notificationManager,
              jobCoordinator,
              progressLock,
              () => activeProgress,
              value => activeProgress = value,
              () => activeProgressJobId,
              value => activeProgressJobId = value,
              () => lastReportUtc,
              value => lastReportUtc = value,
              jobIdToShow,
              primaryTabIndex,
              showToken);
          }

          int safeMaxParallelism = ResolveMaxParallelism(maxParallelism, maxParallelismResolver);

          await Parallel.ForEachAsync(
            batch,
            new ParallelOptions
            {
              MaxDegreeOfParallelism = safeMaxParallelism,
              CancellationToken = cts,
            },
            async (item, token) =>
            {
              jobCoordinator.MarkInFlight(item);

              try
              {
                await createThumbAsync(item, token).ConfigureAwait(false);
              }
              finally
              {
                ThumbnailJobCoordinator.Snapshot snapshot = jobCoordinator.TryComplete(item);

                IDisposable progressToReport;
                int progressJobId;
                lock (progressLock)
                {
                  progressToReport = activeProgress;
                  progressJobId = activeProgressJobId;
                }

                if (progressToReport != null && item.JobId == progressJobId)
                {
                  string reportTitle = $"{GetTabProgressTitle(snapshot.PrimaryTabIndex)} ({snapshot.Completed}/{snapshot.Total})";
                  string message = $"{item.MovieFullPath}";
                  double totalProgress = snapshot.Total > 0
                    ? (double)snapshot.Completed * 100d / snapshot.Total
                    : 0d;
                  if (totalProgress > 100d)
                  {
                    totalProgress = 100d;
                  }

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
                        totalProgress,
                        message,
                        reportTitle).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                      Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-progress] {ex.Message}");
                    }
                  }
                }
              }
            }).ConfigureAwait(false);

          ThumbnailJobCoordinator.Snapshot afterBatch = jobCoordinator.GetSnapshot();
          if (activeProgress != null
            && afterBatch.IsComplete
            && afterBatch.JobId == activeProgressJobId
            && !HasQueuedWorkForJob(queueThumb, queueSync, activeProgressJobId))
          {
            CancelPendingProgressShow();
            await DisposeProgressAsync(uiDispatcher, activeProgress, progressLock).ConfigureAwait(false);
            activeProgress = null;
            activeProgressJobId = -1;
            lastReportUtc = DateTime.MinValue;
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
        CancelPendingProgressShow();
        if (activeProgress != null)
        {
          await DisposeProgressAsync(uiDispatcher, activeProgress, progressLock).ConfigureAwait(false);
        }
      }
    }

    private static async Task ShowProgressAfterDelayAsync(
      Dispatcher uiDispatcher,
      NotificationManager notificationManager,
      ThumbnailJobCoordinator jobCoordinator,
      object progressLock,
      Func<IDisposable> getActiveProgress,
      Action<IDisposable> setActiveProgress,
      Func<int> getActiveProgressJobId,
      Action<int> setActiveProgressJobId,
      Func<DateTime> getLastReportUtc,
      Action<DateTime> setLastReportUtc,
      int jobIdToShow,
      int primaryTabIndex,
      CancellationToken showToken)
    {
      try
      {
        await Task.Delay(ProgressShowDelayMs, showToken).ConfigureAwait(false);

        ThumbnailJobCoordinator.Snapshot snapshot = jobCoordinator.GetSnapshot();
        if (snapshot.JobId != jobIdToShow || snapshot.IsComplete)
        {
          return;
        }

        lock (progressLock)
        {
          if (getActiveProgress() != null || getActiveProgressJobId() != -1)
          {
            return;
          }
        }

        snapshot = jobCoordinator.GetSnapshot();
        if (snapshot.JobId != jobIdToShow || snapshot.IsComplete)
        {
          return;
        }

        IDisposable progress = await ShowProgressOnUiAsync(
          uiDispatcher,
          notificationManager,
          primaryTabIndex).ConfigureAwait(false);

        lock (progressLock)
        {
          if (getActiveProgress() != null)
          {
            progress.Dispose();
            return;
          }

          snapshot = jobCoordinator.GetSnapshot();
          if (snapshot.JobId != jobIdToShow || snapshot.IsComplete)
          {
            progress.Dispose();
            return;
          }

          setActiveProgress(progress);
          setActiveProgressJobId(jobIdToShow);
          setLastReportUtc(DateTime.MinValue);
        }

        string initialTitle = $"{GetTabProgressTitle(primaryTabIndex)} ({snapshot.Completed}/{snapshot.Total})";
        double initialProgress = snapshot.Total > 0
          ? (double)snapshot.Completed * 100d / snapshot.Total
          : 0d;

        try
        {
          await ReportProgressOnUiAsync(
            uiDispatcher,
            progress,
            initialProgress,
            string.Empty,
            initialTitle).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb-progress] {ex.Message}");
        }
      }
      catch (OperationCanceledException)
      {
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

    private static Task<NotificationManager> CreateNotificationManagerAsync(Dispatcher uiDispatcher)
    {
      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        return Task.FromResult(new NotificationManager());
      }

      if (uiDispatcher.CheckAccess())
      {
        return Task.FromResult(new NotificationManager());
      }

      return uiDispatcher.InvokeAsync(() => new NotificationManager()).Task;
    }

    private static Task<IDisposable> ShowProgressOnUiAsync(
      Dispatcher uiDispatcher,
      NotificationManager notificationManager,
      int primaryTabIndex)
    {
      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        return Task.FromResult(CreateProgressBar(notificationManager, primaryTabIndex));
      }

      if (uiDispatcher.CheckAccess())
      {
        return Task.FromResult(CreateProgressBar(notificationManager, primaryTabIndex));
      }

      return uiDispatcher.InvokeAsync(() => CreateProgressBar(notificationManager, primaryTabIndex)).Task;
    }

    private static IDisposable CreateProgressBar(NotificationManager notificationManager, int primaryTabIndex)
    {
      return notificationManager.ShowProgressBar(
        GetTabProgressTitle(primaryTabIndex),
        true,
        false,
        "ProgressArea",
        false,
        2,
        "");
    }

    private static Task ReportProgressOnUiAsync(
      Dispatcher uiDispatcher,
      IDisposable progress,
      double totalProgress,
      string message,
      string reportTitle)
    {
      if (progress == null)
      {
        return Task.CompletedTask;
      }

      void Report()
      {
        dynamic progressReporter = progress;
        progressReporter.Report((totalProgress, message, reportTitle, false));
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

      return uiDispatcher.InvokeAsync(Report, DispatcherPriority.Background).Task;
    }

    private static List<QueueObj> DequeueBatch(ConcurrentQueue<QueueObj> queueThumb, object queueSync)
    {
      lock (queueSync)
      {
        List<QueueObj> batch = [];
        while (queueThumb.TryDequeue(out QueueObj queueObj))
        {
          if (queueObj == null)
          {
            continue;
          }

          batch.Add(queueObj);
        }

        return batch;
      }
    }

    private static Task DisposeProgressAsync(
      Dispatcher uiDispatcher,
      IDisposable progress,
      object progressLock)
    {
      if (progress == null)
      {
        return Task.CompletedTask;
      }

      if (uiDispatcher == null || uiDispatcher.HasShutdownStarted)
      {
        lock (progressLock)
        {
          progress.Dispose();
        }

        return Task.CompletedTask;
      }

      if (uiDispatcher.CheckAccess())
      {
        lock (progressLock)
        {
          progress.Dispose();
        }

        return Task.CompletedTask;
      }

      return uiDispatcher.InvokeAsync(() =>
      {
        lock (progressLock)
        {
          progress.Dispose();
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

    private static string GetTabProgressTitle(int tabIndex)
    {
      return tabIndex switch
      {
        0 => "サムネイル作成中(Small)",
        1 => "サムネイル作成中(Big)",
        2 => "サムネイル作成中(Grid)",
        3 => "サムネイル作成中(List)",
        4 => "サムネイル作成中(Big10)",
        _ => "サムネイル作成中",
      };
    }
  }
}
