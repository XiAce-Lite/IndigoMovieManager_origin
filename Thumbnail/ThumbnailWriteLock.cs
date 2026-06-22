using System.Collections.Concurrent;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 同一サムネイル出力パスへの並行書き込みを直列化する。
    /// </summary>
    internal static class ThumbnailWriteLock
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<IDisposable> AcquireAsync(string thumbPath, CancellationToken cts = default)
        {
            if (string.IsNullOrWhiteSpace(thumbPath))
            {
                return NoopReleaser.Instance;
            }

            SemaphoreSlim semaphore = Locks.GetOrAdd(thumbPath, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cts).ConfigureAwait(false);
            return new Releaser(semaphore);
        }

        private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
        {
            private bool _released;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                semaphore.Release();
            }
        }

        private sealed class NoopReleaser : IDisposable
        {
            public static readonly NoopReleaser Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
