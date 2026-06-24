using System.Collections.Concurrent;
using System.IO;
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

        public List<QueueObj> BuildTabSwitchWork(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration)
        {
            List<QueueObj> work = [];
            if (tabIndex < 0 || tabIndex >= ThumbPathPropertyNames.Length)
            {
                return work;
            }

            foreach (MovieRecords item in filterList)
            {
                if (string.IsNullOrWhiteSpace(item.Movie_Path) || !Path.Exists(item.Movie_Path))
                {
                    continue;
                }

                string fileBody = Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty);
                string hash = !string.IsNullOrWhiteSpace(item.Hash) ? item.Hash : GetHashCRC32(item.Movie_Path);
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

        public void StartTabSwitchJob(
            int tabIndex,
            IEnumerable<MovieRecords> filterList,
            ThumbnailLayoutCache cache,
            string dbFullPath,
            int workGeneration)
        {
            ThumbnailQueueProcessor.RequestDismissProgress();
            ClearQueue();

            List<QueueObj> work = BuildTabSwitchWork(tabIndex, filterList, cache, dbFullPath, workGeneration);

            lock (_sync)
            {
                int jobId = _jobCoordinator.BeginJob(tabIndex);
                if (work.Count == 0)
                {
                    ThumbnailQueueProcessor.RequestDismissProgress();
                    return;
                }

                List<QueueObj> accepted = _jobCoordinator.RegisterWork(jobId, work);
                foreach (QueueObj item in accepted)
                {
                    _queue.Enqueue(item);
                }
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
