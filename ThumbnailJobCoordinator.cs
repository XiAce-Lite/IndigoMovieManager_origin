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
      public int PrimaryTabIndex { get; init; }
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
      public int PrimaryTabIndex { get; init; }
      public int Total { get; set; }
      public int Completed { get; set; }
      public int InFlight { get; set; }
      public bool Abandoned { get; set; }
    }

    private readonly object _lock = new();
    private int _jobId;
    private int _jobSwitchToken;
    private readonly Dictionary<int, JobState> _jobs = [];
    private readonly HashSet<(long MovieId, int TabIndex)> _tracked = [];

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

    public int PrimaryTabIndex
    {
      get
      {
        lock (_lock)
        {
          return _jobs.TryGetValue(_jobId, out JobState state) ? state.PrimaryTabIndex : 0;
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

    public int BeginJob(int primaryTabIndex)
    {
      lock (_lock)
      {
        if (_jobId > 0 && _jobs.TryGetValue(_jobId, out JobState previous))
        {
          previous.Abandoned = true;
        }

        _jobId++;
        _jobSwitchToken++;
        _jobs[_jobId] = new JobState
        {
          PrimaryTabIndex = primaryTabIndex,
        };
        return _jobId;
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

          if (!_tracked.Remove((item.MovieId, item.Tabindex)))
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
        var key = (item.MovieId, item.Tabindex);
        if (!_tracked.Add(key))
        {
          return false;
        }

        item.JobId = SilentJobId;
        return true;
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

          var key = (item.MovieId, item.Tabindex);
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
        if (!_tracked.Remove((item.MovieId, item.Tabindex)))
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
      if (item == null || item.JobId == SilentJobId)
      {
        return;
      }

      lock (_lock)
      {
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
          _tracked.Remove((item.MovieId, item.Tabindex));

          if (item.JobId != SilentJobId && _jobs.TryGetValue(item.JobId, out JobState state))
          {
            state.InFlight = Math.Max(0, state.InFlight - 1);
            state.Completed++;
            TryRemoveFinishedJob(item.JobId, state);
          }
        }

        return CreateSnapshot(reportJobId);
      }
    }

    public bool IsTracked(long movieId, int tabIndex)
    {
      lock (_lock)
      {
        return _tracked.Contains((movieId, tabIndex));
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
      if (jobId == SilentJobId || !_jobs.TryGetValue(jobId, out JobState state))
      {
        return new Snapshot
        {
          JobId = jobId,
          PrimaryTabIndex = 0,
          Total = 0,
          Completed = 0,
          InFlight = 0,
        };
      }

      int total = state.Total;
      int completed = state.Completed;
      if (total < completed)
      {
        total = completed;
      }

      return new Snapshot
      {
        JobId = jobId,
        PrimaryTabIndex = state.PrimaryTabIndex,
        Total = total,
        Completed = completed,
        InFlight = state.InFlight,
        Abandoned = state.Abandoned,
      };
    }

    private void TryRemoveFinishedJob(int jobId, JobState state)
    {
      if (jobId == _jobId)
      {
        return;
      }

      bool finished = state.Abandoned
        ? state.InFlight <= 0
        : state.Total > 0 && state.Completed >= state.Total && state.InFlight <= 0;

      if (finished)
      {
        _jobs.Remove(jobId);
      }
    }
  }
}
