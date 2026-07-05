using System.Collections.Concurrent;
using System.IO;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Services
{
    internal sealed class ThumbnailQueueScheduler
    {
        private readonly ConcurrentQueue<QueueObj> _queue = new();
        private readonly ThumbnailJobCoordinator _jobCoordinator = new();
        private readonly object _sync = new();
        private Task _tabSwitchJobChain = Task.CompletedTask;
        private readonly object _tabSwitchChainGate = new();
        private int _tabSwitchBuildGeneration;

        public int TabSwitchBuildGeneration => Volatile.Read(ref _tabSwitchBuildGeneration);

        public ConcurrentQueue<QueueObj> Queue => _queue;
        public ThumbnailJobCoordinator JobCoordinator => _jobCoordinator;
        public object SyncRoot => _sync;

        public void ClearQueue()
        {
            lock (_sync)
            {
                List<QueueObj> removed = [];
                while (_queue.TryDequeue(out QueueObj obj))
                {
                    if (obj != null)
                    {
                        removed.Add(obj);
                    }
                }

                _jobCoordinator.CancelQueued(removed);
            }
        }

        /// <summary>
        /// タブ切替用。進捗表示付きジョブは残し、サイレントキューのみ破棄する。
        /// </summary>
        public void ClearSilentQueue()
        {
            lock (_sync)
            {
                List<QueueObj> removed = [];
                List<QueueObj> kept = [];
                while (_queue.TryDequeue(out QueueObj obj))
                {
                    if (obj == null)
                    {
                        continue;
                    }

                    if (obj.JobId == ThumbnailJobCoordinator.SilentJobId)
                    {
                        removed.Add(obj);
                    }
                    else
                    {
                        kept.Add(obj);
                    }
                }

                foreach (QueueObj item in kept)
                {
                    _queue.Enqueue(item);
                }

                _jobCoordinator.CancelQueued(removed);
            }
        }

        /// <summary>
        /// DB 切替などで進行中ジョブを破棄し、キューを空にする。
        /// </summary>
        public void AbandonAndClearQueue(string primaryLayoutKey)
        {
            Interlocked.Increment(ref _tabSwitchBuildGeneration);
            ThumbnailQueueProcessor.RequestDismissProgress();

            lock (_sync)
            {
                List<QueueObj> removed = [];
                while (_queue.TryDequeue(out QueueObj obj))
                {
                    if (obj != null)
                    {
                        removed.Add(obj);
                    }
                }

                _jobCoordinator.CancelQueued(removed);
                _jobCoordinator.BeginJob(primaryLayoutKey ?? "");
            }
        }

        public void EnqueueWork(
            IReadOnlyList<QueueObj> items,
            string primaryLayoutKey,
            bool beginNewJob = false,
            string displayTitle = null)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            lock (_sync)
            {
                int jobId = beginNewJob
                    ? _jobCoordinator.BeginJob(primaryLayoutKey ?? "", displayTitle)
                    : _jobCoordinator.CurrentJobId;

                if (!beginNewJob && (jobId == 0 || !_jobCoordinator.IsAcceptingWork(jobId)))
                {
                    jobId = _jobCoordinator.BeginJob(primaryLayoutKey ?? "", displayTitle);
                }

                List<QueueObj> accepted = _jobCoordinator.RegisterWork(jobId, items);
                foreach (QueueObj item in accepted)
                {
                    _queue.Enqueue(item);
                }
            }
        }

        public void EnqueueWork(QueueObj item, string primaryLayoutKey, bool beginNewJob = false) =>
            EnqueueWork([item], primaryLayoutKey, beginNewJob);

        public void EnqueueSilentWork(QueueObj item)
        {
            if (item == null)
            {
                return;
            }

            lock (_sync)
            {
                if (!_jobCoordinator.TryRegisterSilentWork(item))
                {
                    return;
                }

                _queue.Enqueue(item);
            }
        }

        /// <summary>
        /// 詳細タブ（99）用。in-flight でなければ tracked を解除してから silent → 通常ジョブの順で投入する。
        /// </summary>
        public bool TryEnqueueDetailWork(QueueObj item)
        {
            if (item?.ThumbnailLayout == null
                || !item.ThumbnailLayout.Equals(ThumbnailLayoutSpec.DetailPaneLayout))
            {
                return false;
            }

            string layoutKey = ThumbnailLayoutSpec.DetailPaneLayout.Key;

            lock (_sync)
            {
                if (_jobCoordinator.IsInFlight(item.MovieId, layoutKey))
                {
                    return false;
                }

                _jobCoordinator.UntrackIfNotInFlight(item.MovieId, layoutKey);

                if (_jobCoordinator.TryRegisterSilentWork(item))
                {
                    _queue.Enqueue(item);
                    return true;
                }

                int jobId = _jobCoordinator.CurrentJobId;
                if (jobId == 0)
                {
                    jobId = _jobCoordinator.BeginJob(layoutKey);
                }

                List<QueueObj> accepted = _jobCoordinator.RegisterWork(jobId, [item]);
                foreach (QueueObj acceptedItem in accepted)
                {
                    _queue.Enqueue(acceptedItem);
                }

                return accepted.Count > 0;
            }
        }

        public bool TryEnqueueManualWork(QueueObj item)
        {
            if (item == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (!_jobCoordinator.TryRegisterManualWork(item))
                {
                    return false;
                }

                _queue.Enqueue(item);
                return true;
            }
        }

        public void CancelTrackedForMovie(long movieId) =>
            _jobCoordinator.CancelTrackedForMovie(movieId);

        public void ClearTrackingForLayoutKey(string layoutKey) =>
            _jobCoordinator.ClearTrackingForLayoutKey(layoutKey);

        public List<QueueObj> BuildTabSwitchWork(
            ThumbnailLayoutSpec layout,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch = -1)
        {
            List<QueueObj> work = [];
            if (layout == null || cache == null)
            {
                return work;
            }

            ThumbnailHashSyncContext hashSyncContext = ThumbnailHashSync.ForDatabase(dbFullPath);

            foreach (MovieRecords item in filterList)
            {
                if (!IsTabSwitchBuildCurrent(buildEpoch))
                {
                    return work;
                }

                if (!ShouldEnqueueTabSwitchWork(item, layout, cache, hashSyncContext))
                {
                    continue;
                }

                work.Add(new QueueObj
                {
                    MovieId = item.Movie_Id,
                    MovieFullPath = item.Movie_Path,
                    ThumbnailLayout = layout,
                    DbFullPath = dbFullPath,
                    WorkGeneration = workGeneration,
                });
            }

            return work;
        }

        private bool IsTabSwitchBuildCurrent(int buildEpoch) =>
            buildEpoch < 0 || buildEpoch == Volatile.Read(ref _tabSwitchBuildGeneration);

        private static bool ShouldEnqueueTabSwitchWork(
            MovieRecords item,
            ThumbnailLayoutSpec layout,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext hashSyncContext)
        {
            return ThumbnailHashSync.ShouldEnqueueAfterHashSync(item, layout, cache, hashSyncContext);
        }

        /// <summary>
        /// 監視で投入済みのジョブをタブ切替スキャンが beginNewJob で捨てないための判定。
        /// </summary>
        public bool ShouldBeginNewVisibleJob(string layoutKey)
        {
            ThumbnailJobCoordinator.Snapshot snapshot = _jobCoordinator.GetSnapshot();
            if (!string.IsNullOrEmpty(layoutKey)
                && !string.IsNullOrEmpty(snapshot.PrimaryLayoutKey)
                && !string.Equals(snapshot.PrimaryLayoutKey, layoutKey, StringComparison.Ordinal))
            {
                return true;
            }

            return snapshot.Total <= 0
                || snapshot.IsComplete
                || snapshot.Abandoned;
        }

        public void StartTabSwitchJob(
            ThumbnailLayoutSpec layout,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch,
            string displayTitle = null,
            Action onFirstBatchEnqueued = null,
            Action onScanCompleted = null)
        {
            lock (_tabSwitchChainGate)
            {
                _tabSwitchJobChain = RunTabSwitchJobAfterAsync(
                    _tabSwitchJobChain,
                    layout,
                    filterList,
                    cache,
                    dbFullPath,
                    workGeneration,
                    buildEpoch,
                    displayTitle,
                    onFirstBatchEnqueued,
                    onScanCompleted);
            }
        }

        public void StartTabSwitchJob(
            ThumbnailLayoutSpec layout,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch) =>
            StartTabSwitchJob(
                layout,
                filterList,
                cache,
                dbFullPath,
                workGeneration,
                buildEpoch,
                null,
                null,
                null);

        private async Task RunTabSwitchJobAfterAsync(
            Task prior,
            ThumbnailLayoutSpec layout,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch,
            string displayTitle,
            Action onFirstBatchEnqueued,
            Action onScanCompleted)
        {
            try
            {
                await prior.ConfigureAwait(false);
            }
            catch
            {
                // 先行ジョブの失敗で後続のタブ切替サムネ投入を止めない。
            }

            await StartTabSwitchJobAsync(
                layout,
                filterList,
                cache,
                dbFullPath,
                workGeneration,
                buildEpoch,
                displayTitle,
                onFirstBatchEnqueued,
                onScanCompleted).ConfigureAwait(false);
        }

        public async Task StartTabSwitchJobAsync(
            ThumbnailLayoutSpec layout,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch,
            string displayTitle = null,
            Action onFirstBatchEnqueued = null,
            Action onScanCompleted = null)
        {
            if (!IsTabSwitchBuildCurrent(buildEpoch) || layout == null)
            {
                onScanCompleted?.Invoke();
                return;
            }

            ClearSilentQueue();

            IReadOnlyList<MovieRecords> snapshot = filterList as IReadOnlyList<MovieRecords> ?? [.. filterList];

            ThumbnailHashSyncContext hashSyncContext = ThumbnailHashSync.ForDatabase(dbFullPath);

            await Task.Run(() =>
            {
                object batchLock = new();
                List<QueueObj> batch = new(capacity: 64);
                bool jobStarted = false;
                bool firstBatchNotified = false;

                Parallel.ForEach(
                    snapshot,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 2, 8),
                    },
                    item =>
                    {
                        if (!IsTabSwitchBuildCurrent(buildEpoch))
                        {
                            return;
                        }

                        if (!ShouldEnqueueTabSwitchWork(item, layout, cache, hashSyncContext))
                        {
                            return;
                        }

                        List<QueueObj> toFlush = null;
                        lock (batchLock)
                        {
                            batch.Add(new QueueObj
                            {
                                MovieId = item.Movie_Id,
                                MovieFullPath = item.Movie_Path,
                                ThumbnailLayout = layout,
                                DbFullPath = dbFullPath,
                                WorkGeneration = workGeneration,
                            });

                            const int threshold = 64;
                            if (batch.Count >= threshold)
                            {
                                toFlush = batch;
                                batch = new List<QueueObj>(capacity: 64);
                            }
                        }

                        if (toFlush != null
                            && TryEnqueueTabSwitchBatch(
                                toFlush,
                                layout.Key,
                                buildEpoch,
                                displayTitle,
                                ref jobStarted))
                        {
                            if (!firstBatchNotified)
                            {
                                firstBatchNotified = true;
                                onFirstBatchEnqueued?.Invoke();
                            }
                        }
                    });

                List<QueueObj> remainder;
                lock (batchLock)
                {
                    remainder = batch;
                    batch = null;
                }

                if (remainder != null
                    && remainder.Count > 0
                    && TryEnqueueTabSwitchBatch(
                        remainder,
                        layout.Key,
                        buildEpoch,
                        displayTitle,
                        ref jobStarted)
                    && !firstBatchNotified)
                {
                    onFirstBatchEnqueued?.Invoke();
                }
            }).ConfigureAwait(false);

            onScanCompleted?.Invoke();
        }

        private bool TryEnqueueTabSwitchBatch(
            List<QueueObj> batch,
            string layoutKey,
            int buildEpoch,
            string displayTitle,
            ref bool jobStarted)
        {
            if (!IsTabSwitchBuildCurrent(buildEpoch) || batch == null || batch.Count == 0)
            {
                return false;
            }

            lock (_sync)
            {
                if (!IsTabSwitchBuildCurrent(buildEpoch))
                {
                    return false;
                }

                if (!jobStarted)
                {
                    // 監視フォルダの新規投入が先に走っている場合は追記し、ジョブを破棄しない。
                    if (ShouldBeginNewVisibleJob(layoutKey))
                    {
                        ClearTrackingForLayoutKey(layoutKey);
                        EnqueueWork(batch, layoutKey, beginNewJob: true, displayTitle);
                    }
                    else
                    {
                        EnqueueWork(batch, layoutKey, beginNewJob: false, displayTitle);
                    }

                    jobStarted = true;
                }
                else
                {
                    EnqueueWork(batch, layoutKey, beginNewJob: false, displayTitle);
                }
            }

            return true;
        }

        public static int GetMaxParallelism()
        {
            return ThumbnailQueueProcessor.ClampThumbnailParallelism(
                Properties.Settings.Default.ThumbnailParallelism
            );
        }
    }
}
