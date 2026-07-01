using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
  /// <summary>
  /// サムネイル作成ジョブの進捗とキュー重複管理を行う。
  /// </summary>
  public sealed class ThumbnailJobCoordinator
  {
    public const int SilentJobId = -1;

        public readonly struct Snapshot
    {
      public int JobId { get; init; }
      public string PrimaryLayoutKey { get; init; }
      public string DisplayTitle { get; init; }
      public int Total { get; init; }
      public int Completed { get; init; }
      public int InFlight { get; init; }
      public bool Abandoned { get; init; }

      public bool IsComplete =>
        (Abandoned && InFlight <= 0)
        || (Total > 0 && Completed >= Total && InFlight <= 0);
    }

    private sealed class JobState
    {
      public string PrimaryLayoutKey { get; init; } = "";
      public string DisplayTitle { get; init; } = "";
      public int Total { get; set; }
      public int Completed { get; set; }
      public int InFlight { get; set; }
      public bool Abandoned { get; set; }
    }

    private readonly object _lock = new();
    private int _jobId;
    private int _jobSwitchToken;
    private readonly Dictionary<int, JobState> _jobs = [];
    private readonly Dictionary<int, CancellationTokenSource> _jobCancellation = [];
    private readonly HashSet<(long MovieId, string LayoutKey)> _tracked = [];
    private readonly HashSet<(long MovieId, string LayoutKey)> _inFlight = [];

    private static (long MovieId, string LayoutKey) TrackKey(QueueObj item) =>
        (item.MovieId, ThumbnailLayoutResolver.GetTrackLayoutKey(item));

    public int CurrentJobId
    {
      get
      {
        lock (_lock)
        {
          return _jobId;
        }
      }
    }

    public string PrimaryLayoutKey
    {
      get
      {
        lock (_lock)
        {
          return _jobs.TryGetValue(_jobId, out JobState state) ? state.PrimaryLayoutKey : "";
        }
      }
    }

    public int JobSwitchToken
    {
      get
      {
        lock (_lock)
        {
          return _jobSwitchToken;
        }
      }
    }

    public int BeginJob(string primaryLayoutKey, string displayTitle = null)
    {
      lock (_lock)
      {
        if (_jobId > 0 && _jobs.TryGetValue(_jobId, out JobState previous))
        {
          previous.Abandoned = true;
          CancelJobCancellationLocked(_jobId);
        }

        _jobId++;
        _jobSwitchToken++;
        _jobs[_jobId] = new JobState
        {
          PrimaryLayoutKey = primaryLayoutKey ?? "",
          DisplayTitle = displayTitle ?? "",
        };
        _jobCancellation[_jobId] = new CancellationTokenSource();
        return _jobId;
      }
    }

    public int BeginJob(string primaryLayoutKey) =>
      BeginJob(primaryLayoutKey, null);

    /// <summary>
    /// タブ/スキン切替でジョブが放棄されたとき、実行中ワーカーへキャンセルを伝える。
    /// </summary>
    public CancellationToken GetJobCancellationToken(int jobId)
    {
      if (jobId <= 0 || jobId == SilentJobId)
      {
        return CancellationToken.None;
      }

      lock (_lock)
      {
        return _jobCancellation.TryGetValue(jobId, out CancellationTokenSource cts)
          ? cts.Token
          : CancellationToken.None;
      }
    }

    private void CancelJobCancellationLocked(int jobId)
    {
      if (!_jobCancellation.TryGetValue(jobId, out CancellationTokenSource cts))
      {
        return;
      }

      try
      {
        cts.Cancel();
      }
      catch (ObjectDisposedException)
      {
      }
    }

    private void DisposeJobCancellationLocked(int jobId)
    {
      if (_jobCancellation.Remove(jobId, out CancellationTokenSource cts))
      {
        cts.Dispose();
      }
    }

    public void CancelQueued(IReadOnlyList<QueueObj> removed)
    {
      if (removed == null || removed.Count == 0)
      {
        return;
      }

      lock (_lock)
      {
        foreach (QueueObj item in removed)
        {
          if (item == null)
          {
            continue;
          }

          if (!_tracked.Remove(TrackKey(item)))
          {
            continue;
          }

          if (_jobs.TryGetValue(item.JobId, out JobState state))
          {
            state.Total = Math.Max(state.Completed, state.Total - 1);
            TryRemoveFinishedJob(item.JobId, state);
          }
        }
      }
    }

    /// <summary>
    /// 進捗表示なし（クリック時の詳細サムネなど）。
    /// </summary>
        public bool TryRegisterSilentWork(QueueObj item)
        {
            if (item == null)
            {
                return false;
            }

            lock (_lock)
            {
                var key = TrackKey(item);
                if (_inFlight.Contains(key))
                {
                    return false;
                }

                _tracked.Remove(key);
                if (!_tracked.Add(key))
                {
                    return false;
                }

                item.JobId = SilentJobId;
                return true;
            }
        }

    /// <summary>
    /// 手動サムネ用。in-flight 中でなければ tracked を更新してキュー投入可能にする。
    /// </summary>
    public bool TryRegisterManualWork(QueueObj item)
    {
      if (item == null)
      {
        return false;
      }

      lock (_lock)
      {
        var key = TrackKey(item);
        if (_inFlight.Contains(key))
        {
          return false;
        }

        _tracked.Remove(key);
        if (!_tracked.Add(key))
        {
          return false;
        }

        item.JobId = SilentJobId;
        return true;
      }
    }

    /// <summary>
    /// 指定レイアウトの追跡（tracked / inFlight）をまとめて解除する。
    /// </summary>
    public void ClearTrackingForLayoutKey(string layoutKey)
    {
      if (string.IsNullOrEmpty(layoutKey))
      {
        return;
      }

      lock (_lock)
      {
        _tracked.RemoveWhere(k => k.LayoutKey == layoutKey);
        _inFlight.RemoveWhere(k => k.LayoutKey == layoutKey);
      }
    }

    public void CancelTrackedForMovie(long movieId)
    {
      lock (_lock)
      {
        List<(long MovieId, string LayoutKey)> keys = [.. _tracked.Where(k => k.MovieId == movieId)];
        foreach ((long id, string layoutKey) in keys)
        {
          if (_inFlight.Contains((id, layoutKey)))
          {
            continue;
          }

          _tracked.Remove((id, layoutKey));
        }
      }
    }

    public bool IsInFlight(long movieId, string layoutKey)
    {
      lock (_lock)
      {
        return _inFlight.Contains((movieId, layoutKey));
      }
    }

    public void UntrackIfNotInFlight(long movieId, string layoutKey)
    {
      lock (_lock)
      {
        var key = (movieId, layoutKey);
        if (!_inFlight.Contains(key))
        {
          _tracked.Remove(key);
        }
      }
    }

    public List<QueueObj> RegisterWork(int jobId, IReadOnlyList<QueueObj> items)
    {
      List<QueueObj> accepted = [];
      if (items == null || items.Count == 0)
      {
        return accepted;
      }

      lock (_lock)
      {
        foreach (QueueObj item in items)
        {
          if (item == null)
          {
            continue;
          }

          var key = TrackKey(item);
          if (!_tracked.Add(key))
          {
            continue;
          }

          item.JobId = jobId;
          if (_jobs.TryGetValue(jobId, out JobState state))
          {
            state.Total++;
          }

          accepted.Add(item);
        }
      }

      return accepted;
    }

    public bool ShouldProcess(QueueObj item)
    {
      if (item == null)
      {
        return false;
      }

      if (item.JobId == SilentJobId)
      {
        return true;
      }

      lock (_lock)
      {
        return item.JobId == _jobId
          && _jobs.TryGetValue(item.JobId, out JobState state)
          && !state.Abandoned;
      }
    }

    /// <summary>
    /// タブ切替などで不要になったキュー内アイテムを破棄する（進捗の Completed は増やさない）。
    /// </summary>
    public void TrySkipItem(QueueObj item)
    {
      if (item == null || item.JobId == SilentJobId)
      {
        return;
      }

      lock (_lock)
      {
        if (!_tracked.Remove(TrackKey(item)))
        {
          return;
        }

        if (_jobs.TryGetValue(item.JobId, out JobState state))
        {
          state.Total = Math.Max(state.Completed, state.Total - 1);
          TryRemoveFinishedJob(item.JobId, state);
        }
      }
    }

    public void MarkInFlight(QueueObj item)
    {
      if (item == null)
      {
        return;
      }

      lock (_lock)
      {
        _inFlight.Add(TrackKey(item));

        if (item.JobId == SilentJobId)
        {
          return;
        }

        if (_jobs.TryGetValue(item.JobId, out JobState state))
        {
          state.InFlight++;
        }
      }
    }

    /// <summary>
    /// 完了処理を行い、完了したアイテムのジョブ単位スナップショットを返す。
    /// </summary>
    public Snapshot TryComplete(QueueObj item)
    {
      lock (_lock)
      {
        int reportJobId = _jobId;

        if (item != null)
        {
          reportJobId = item.JobId;
          _tracked.Remove(TrackKey(item));
          _inFlight.Remove(TrackKey(item));

          if (item.JobId != SilentJobId && _jobs.TryGetValue(item.JobId, out JobState state))
          {
            state.InFlight = Math.Max(0, state.InFlight - 1);
            state.Completed++;
            Snapshot snapshot = CreateSnapshot(item.JobId);
            TryRemoveFinishedJob(item.JobId, state);
            return snapshot;
          }
        }

        return CreateSnapshot(reportJobId);
      }
    }

    public bool IsTracked(long movieId, string layoutKey)
    {
      lock (_lock)
      {
        return _tracked.Contains((movieId, layoutKey));
      }
    }

    public Snapshot GetSnapshot()
    {
      lock (_lock)
      {
        return CreateSnapshot(_jobId);
      }
    }

    public Snapshot GetSnapshot(int jobId)
    {
      lock (_lock)
      {
        return CreateSnapshot(jobId);
      }
    }

    private Snapshot CreateSnapshot(int jobId)
    {
      if (jobId <= 0 || !_jobs.TryGetValue(jobId, out JobState state))
      {
        return new Snapshot { JobId = jobId };
      }

      return new Snapshot
      {
        JobId = jobId,
        PrimaryLayoutKey = state.PrimaryLayoutKey,
        DisplayTitle = state.DisplayTitle,
        Total = state.Total,
        Completed = state.Completed,
        InFlight = state.InFlight,
        Abandoned = state.Abandoned,
      };
    }

    private void TryRemoveFinishedJob(int jobId, JobState state)
    {
      if (state.Abandoned && state.InFlight <= 0)
      {
        _jobs.Remove(jobId);
        DisposeJobCancellationLocked(jobId);
        return;
      }

      if (state.Total > 0 && state.Completed >= state.Total && state.InFlight <= 0)
      {
        _jobs.Remove(jobId);
        DisposeJobCancellationLocked(jobId);
      }
    }
  }
}
