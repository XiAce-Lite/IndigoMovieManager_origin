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
        private static readonly string[] ThumbPathPropertyNames =
        [
            nameof(MovieRecords.ThumbPathSmall),
            nameof(MovieRecords.ThumbPathBig),
            nameof(MovieRecords.ThumbPathGrid),
            nameof(MovieRecords.ThumbPathList),
            nameof(MovieRecords.ThumbPathBig10),
        ];

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
        public void AbandonAndClearQueue(int primaryTabIndex = 0)
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
                _jobCoordinator.BeginJob(primaryTabIndex);
            }
        }

        public void EnqueueWork(IReadOnlyList<QueueObj> items, int primaryTabIndex, bool beginNewJob = false)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            lock (_sync)
            {
                int jobId = beginNewJob
                    ? _jobCoordinator.BeginJob(primaryTabIndex)
                    : _jobCoordinator.CurrentJobId;

                if (!beginNewJob && jobId == 0)
                {
                    jobId = _jobCoordinator.BeginJob(primaryTabIndex);
                }

                List<QueueObj> accepted = _jobCoordinator.RegisterWork(jobId, items);
                foreach (QueueObj item in accepted)
                {
                    _queue.Enqueue(item);
                }
            }
        }

        public void EnqueueWork(QueueObj item, int primaryTabIndex, bool beginNewJob = false)
        {
            EnqueueWork([item], primaryTabIndex, beginNewJob);
        }

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
            if (item == null || item.Tabindex != 99)
            {
                return false;
            }

            lock (_sync)
            {
                if (_jobCoordinator.IsInFlight(item.MovieId, 99))
                {
                    return false;
                }

                _jobCoordinator.UntrackIfNotInFlight(item.MovieId, 99);

                if (_jobCoordinator.TryRegisterSilentWork(item))
                {
                    _queue.Enqueue(item);
                    return true;
                }

                int jobId = _jobCoordinator.CurrentJobId;
                if (jobId == 0)
                {
                    jobId = _jobCoordinator.BeginJob(99);
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

        public void ClearTrackingForTab(int tabIndex) =>
            _jobCoordinator.ClearTrackingForTab(tabIndex);

        public List<QueueObj> BuildTabSwitchWork(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch = -1)
        {
            if (tabIndex == SkinTabIndexHelper.WpfSkinThumbnailSlotIndex)
            {
                return BuildWpfSkinTabSwitchWork(filterList, cache, dbFullPath, workGeneration, buildEpoch);
            }

            List<QueueObj> work = [];
            if (tabIndex < 0 || tabIndex >= ThumbPathPropertyNames.Length)
            {
                return work;
            }

            foreach (MovieRecords item in filterList)
            {
                if (!IsTabSwitchBuildCurrent(buildEpoch))
                {
                    return work;
                }

                if (!ShouldEnqueueTabSwitchWork(item, tabIndex, cache, out _))
                {
                    continue;
                }

                work.Add(new QueueObj
                {
                    MovieId = item.Movie_Id,
                    MovieFullPath = item.Movie_Path,
                    Tabindex = tabIndex,
                    DbFullPath = dbFullPath,
                    WorkGeneration = workGeneration,
                });
            }

            return work;
        }

        private List<QueueObj> BuildWpfSkinTabSwitchWork(
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch)
        {
            List<QueueObj> work = [];
            ThumbnailLayoutSpec spec = WpfSkinSettings.CurrentThumbnailLayout;
            if (spec == null || cache == null)
            {
                return work;
            }

            int tabIndex = SkinTabIndexHelper.WpfSkinThumbnailSlotIndex;
            foreach (MovieRecords item in filterList)
            {
                if (!IsTabSwitchBuildCurrent(buildEpoch))
                {
                    return work;
                }

                if (!ShouldEnqueueTabSwitchWork(item, tabIndex, cache, out ThumbnailLayoutSpec layoutSpec))
                {
                    continue;
                }

                work.Add(new QueueObj
                {
                    MovieId = item.Movie_Id,
                    MovieFullPath = item.Movie_Path,
                    Tabindex = tabIndex,
                    ThumbnailLayout = layoutSpec,
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
            int tabIndex,
            ThumbnailLayoutCache cache,
            out ThumbnailLayoutSpec wpfSpec)
        {
            wpfSpec = null;
            if (item == null
                || cache == null
                || string.IsNullOrWhiteSpace(item.Movie_Path)
                || string.IsNullOrWhiteSpace(item.Hash)
                || !File.Exists(item.Movie_Path))
            {
                return false;
            }

            string fileBody = Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty)
                .ToLowerInvariant();

            if (tabIndex == SkinTabIndexHelper.WpfSkinThumbnailSlotIndex)
            {
                wpfSpec = WpfSkinSettings.CurrentThumbnailLayout;
                if (wpfSpec == null)
                {
                    return false;
                }

                string expectedPath = cache.GetExpectedThumbPath(wpfSpec, fileBody, item.Hash);
                return NeedsThumbnailGeneration(item, expectedPath, tabIndex, cache);
            }

            if (tabIndex < 0 || tabIndex >= cache.TabOutPaths.Length)
            {
                return false;
            }

            string slotPath = cache.GetExpectedThumbPath(tabIndex, fileBody, item.Hash);
            return NeedsThumbnailGeneration(item, slotPath, tabIndex, cache);
        }

        private static bool NeedsThumbnailGeneration(
            MovieRecords item,
            string expectedPath,
            int tabIndex,
            ThumbnailLayoutCache cache)
        {
            if (string.IsNullOrWhiteSpace(expectedPath))
            {
                return true;
            }

            if (!File.Exists(expectedPath))
            {
                return true;
            }

            if (tabIndex >= 0 && tabIndex < cache.TabOutPaths.Length)
            {
                return ThumbnailTabErrorDetector.IsErrorForTab(item, tabIndex, cache);
            }

            return !ThumbnailValidityHelper.LooksLikeCompositeThumbnail(expectedPath);
        }

        public void StartTabSwitchJob(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch)
        {
            lock (_tabSwitchChainGate)
            {
                _tabSwitchJobChain = RunTabSwitchJobAfterAsync(
                    _tabSwitchJobChain,
                    tabIndex,
                    filterList,
                    cache,
                    dbFullPath,
                    workGeneration,
                    buildEpoch);
            }
        }

        private async Task RunTabSwitchJobAfterAsync(
            Task prior,
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch)
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
                tabIndex,
                filterList,
                cache,
                dbFullPath,
                workGeneration,
                buildEpoch).ConfigureAwait(false);
        }

        public async Task StartTabSwitchJobAsync(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration,
            int buildEpoch)
        {
            if (!IsTabSwitchBuildCurrent(buildEpoch))
            {
                return;
            }

            ClearSilentQueue();

            IReadOnlyList<MovieRecords> snapshot = filterList as IReadOnlyList<MovieRecords> ?? [.. filterList];
            List<QueueObj> work = await Task.Run(() =>
                BuildTabSwitchWork(tabIndex, snapshot, cache, dbFullPath, workGeneration, buildEpoch)).ConfigureAwait(false);

            if (!IsTabSwitchBuildCurrent(buildEpoch))
            {
                return;
            }

            if (work.Count == 0)
            {
                return;
            }

            lock (_sync)
            {
                if (!IsTabSwitchBuildCurrent(buildEpoch))
                {
                    return;
                }

                ClearTrackingForTab(tabIndex);
                EnqueueWork(work, tabIndex, beginNewJob: true);
            }
        }

        public static int GetMaxParallelism()
        {
            return ThumbnailQueueProcessor.ClampThumbnailParallelism(
                Properties.Settings.Default.ThumbnailParallelism
            );
        }
    }
}
