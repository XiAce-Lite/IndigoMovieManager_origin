using Notification.Wpf;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace IndigoMovieManager
{
    /// <summary>
    /// サムネイル作成キューの監視・進捗表示・並列実行を担当する。
    /// </summary>
    public sealed class ThumbnailQueueProcessor
    {
        public async Task RunAsync(
            ConcurrentQueue<QueueObj> queueThumb,
            Func<QueueObj, CancellationToken, Task> createThumbAsync,
            int maxParallelism = 4,
            Func<int> maxParallelismResolver = null,
            int pollIntervalMs = 3000,
            Action<string> log = null,
            CancellationToken cts = default)
        {
            var title = "サムネイル作成中";
            NotificationManager notificationManager = new();
            int safePollIntervalMs = pollIntervalMs < 100 ? 100 : pollIntervalMs;

            try
            {
                while (true)
                {
                    await Task.Delay(safePollIntervalMs, cts);
                    if (queueThumb.IsEmpty) { continue; }

                    List<QueueObj> batch = [];
                    while (queueThumb.TryDequeue(out QueueObj queueObj))
                    {
                        if (queueObj == null) { continue; }
                        batch.Add(queueObj);
                    }
                    if (batch.Count < 1) { continue; }

                    int safeMaxParallelism = ResolveMaxParallelism(maxParallelism, maxParallelismResolver);

                    var progress = notificationManager.ShowProgressBar(title, false, true, "ProgressArea", false, 2, "");
                    object progressLock = new();
                    int completedCount = 0;
                    int totalCount = batch.Count;

                    await Parallel.ForEachAsync(
                        batch,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = safeMaxParallelism,
                            CancellationToken = cts,
                        },
                        async (item, token) =>
                        {
                            await createThumbAsync(item, token).ConfigureAwait(false);

                            int done = Interlocked.Increment(ref completedCount);
                            var reportTitle = $"{GetTabProgressTitle(item.Tabindex)} ({done}/{totalCount})";
                            var message = $"{item.MovieFullPath}";
                            double totalProgress = (double)done * 100d / totalCount;
                            if (totalProgress > 100d) { totalProgress = 100d; }

                            lock (progressLock)
                            {
                                progress.Report((totalProgress, message, reportTitle, false));
                            }
                        });

                    lock (progressLock)
                    {
                        progress.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
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
