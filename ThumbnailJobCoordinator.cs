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

      public bool IsComplete => Total > 0 && Completed >= Total && InFlight <= 0;
    }

    private readonly object _lock = new();
    private int _jobId;
    private int _primaryTabIndex;
    private int _total;
    private int _completed;
    private int _inFlight;
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
          return _primaryTabIndex;
        }
      }
    }

    public int BeginJob(int primaryTabIndex)
    {
      lock (_lock)
      {
        _jobId++;
        _primaryTabIndex = primaryTabIndex;
        _total = 0;
        _completed = 0;
        _inFlight = 0;
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

          if (item.JobId == _jobId)
          {
            _total = Math.Max(0, _total - 1);
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
          if (jobId == _jobId)
          {
            _total++;
          }

          accepted.Add(item);
        }
      }

      return accepted;
    }

    public void MarkInFlight(QueueObj item)
    {
      if (item == null)
      {
        return;
      }

      lock (_lock)
      {
        if (item.JobId == _jobId)
        {
          _inFlight++;
        }
      }
    }

    public Snapshot TryComplete(QueueObj item)
    {
      lock (_lock)
      {
        if (item != null)
        {
          _tracked.Remove((item.MovieId, item.Tabindex));
          if (item.JobId == _jobId)
          {
            _inFlight = Math.Max(0, _inFlight - 1);
            _completed++;
          }
        }

        return CreateSnapshot();
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
        return CreateSnapshot();
      }
    }

    private Snapshot CreateSnapshot()
    {
      int total = _total;
      int completed = _completed;
      if (total < completed)
      {
        total = completed;
      }

      return new Snapshot
      {
        JobId = _jobId,
        PrimaryTabIndex = _primaryTabIndex,
        Total = total,
        Completed = completed,
        InFlight = _inFlight,
      };
    }
  }
}
