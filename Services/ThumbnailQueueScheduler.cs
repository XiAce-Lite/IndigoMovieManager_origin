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
            int workGeneration)
        {
            if (tabIndex == SkinTabIndexHelper.WpfSkinThumbnailSlotIndex)
            {
                return BuildWpfSkinTabSwitchWork(filterList, cache, dbFullPath, workGeneration);
            }

            List<QueueObj> work = [];
            if (tabIndex < 0 || tabIndex >= ThumbPathPropertyNames.Length)
            {
                return work;
            }

            foreach (MovieRecords item in filterList)
            {
                if (string.IsNullOrWhiteSpace(item.Movie_Path) || string.IsNullOrWhiteSpace(item.Hash))
                {
                    continue;
                }

                if (!File.Exists(item.Movie_Path))
                {
                    continue;
                }

                string fileBody = Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty)
                    .ToLowerInvariant();
                string hash = item.Hash;
                string expectedPath = cache.GetExpectedThumbPath(tabIndex, fileBody, hash);
                if (File.Exists(expectedPath))
                {
                    continue;
                }

                if (!_jobCoordinator.IsTracked(item.Movie_Id, tabIndex))
                {
                    work.Add(new QueueObj
                    {
                        MovieId = item.Movie_Id,
                        MovieFullPath = item.Movie_Path,
                        Tabindex = tabIndex,
                        DbFullPath = dbFullPath,
                        WorkGeneration = workGeneration,
                    });
                }
            }

            return work;
        }

        private List<QueueObj> BuildWpfSkinTabSwitchWork(
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration)
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
                if (string.IsNullOrWhiteSpace(item.Movie_Path) || string.IsNullOrWhiteSpace(item.Hash))
                {
                    continue;
                }

                if (!File.Exists(item.Movie_Path))
                {
                    continue;
                }

                string fileBody = Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty)
                    .ToLowerInvariant();
                string expectedPath = cache.GetExpectedThumbPath(spec, fileBody, item.Hash);
                if (File.Exists(expectedPath))
                {
                    continue;
                }

                if (!_jobCoordinator.IsTracked(item.Movie_Id, tabIndex))
                {
                    work.Add(new QueueObj
                    {
                        MovieId = item.Movie_Id,
                        MovieFullPath = item.Movie_Path,
                        Tabindex = tabIndex,
                        ThumbnailLayout = spec,
                        DbFullPath = dbFullPath,
                        WorkGeneration = workGeneration,
                    });
                }
            }

            return work;
        }

        public void StartTabSwitchJob(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration)
        {
            _ = StartTabSwitchJobAsync(tabIndex, filterList, cache, dbFullPath, workGeneration);
        }

        public async Task StartTabSwitchJobAsync(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration)
        {
            ClearSilentQueue();

            IReadOnlyList<MovieRecords> snapshot = filterList as IReadOnlyList<MovieRecords> ?? [.. filterList];
            List<QueueObj> work = await Task.Run(() =>
                BuildTabSwitchWork(tabIndex, snapshot, cache, dbFullPath, workGeneration)).ConfigureAwait(false);

            if (work.Count == 0)
            {
                return;
            }

            EnqueueWork(work, tabIndex, beginNewJob: true);
        }

        public static int GetMaxParallelism()
        {
            return ThumbnailQueueProcessor.ClampThumbnailParallelism(
                Properties.Settings.Default.ThumbnailParallelism
            );
        }
    }
}
