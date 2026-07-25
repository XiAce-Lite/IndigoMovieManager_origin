using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// ジャケ写 URL の非同期読込（詳細パネル・一覧 preferJacket 共用）。
    /// HttpClient でバイト取得（タイムアウト付き）し、メモリから Bitmap を組み立てる。
    /// BitmapImage の UriSource 待ちは完了イベントが来ないとハングするため使わない。
    /// </summary>
    internal static class DmmRemoteImageLoader
    {
        private static readonly ConcurrentDictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Task<BitmapSource>> InFlight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(15);
        private static readonly HttpClient Http = CreateClient();

        private static CancellationTokenSource _gate = new();
        private static int _session;
        private static int _inFlightCount;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IndigoMovieManager/1.0");
            return client;
        }

        public static bool TryGetCached(string url, out BitmapSource image)
        {
            image = null;
            if (!DmmJacketUrls.IsHttpUrl(url))
            {
                return false;
            }

            return Cache.TryGetValue(url.Trim(), out image) && image != null;
        }

        /// <summary>
        /// 進行中の取得を破棄し、ステータスバー件数を 0 に戻す（スキン切替時など）。
        /// メモリキャッシュは維持する。
        /// </summary>
        public static void CancelPendingAndResetProgress()
        {
            Interlocked.Increment(ref _session);

            CancellationTokenSource previous = Interlocked.Exchange(ref _gate, new CancellationTokenSource());
            try
            {
                previous.Cancel();
            }
            catch
            {
            }

            try
            {
                previous.Dispose();
            }
            catch
            {
            }

            InFlight.Clear();
            Interlocked.Exchange(ref _inFlightCount, 0);
            ReportInFlight(0);
        }

        public static Task<BitmapSource> LoadAsync(string url, Dispatcher dispatcher)
        {
            if (dispatcher == null || !DmmJacketUrls.IsHttpUrl(url))
            {
                return Task.FromResult<BitmapSource>(null);
            }

            string key = url.Trim();
            if (Cache.TryGetValue(key, out BitmapSource cached) && cached != null)
            {
                return Task.FromResult(cached);
            }

            return InFlight.GetOrAdd(key, _ => LoadCoreAsync(key, dispatcher));
        }

        private static async Task<BitmapSource> LoadCoreAsync(string url, Dispatcher dispatcher)
        {
            int session = Volatile.Read(ref _session);
            CancellationToken gateToken = _gate.Token;

            int count = Interlocked.Increment(ref _inFlightCount);
            if (session == Volatile.Read(ref _session))
            {
                ReportInFlight(count);
            }

            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(gateToken);
                linked.CancelAfter(LoadTimeout);
                CancellationToken ct = linked.Token;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    ct.ThrowIfCancellationRequested();

                    if (attempt > 0)
                    {
                        await Task.Delay(200 * attempt, ct).ConfigureAwait(false);
                    }

                    BitmapSource image = await LoadOnceAsync(url, dispatcher, ct).ConfigureAwait(false);
                    if (image != null)
                    {
                        Cache[url] = image;
                        return image;
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                InFlight.TryRemove(url, out _);
                if (session == Volatile.Read(ref _session))
                {
                    int remaining = Interlocked.Decrement(ref _inFlightCount);
                    ReportInFlight(Math.Max(0, remaining));
                }
            }
        }

        private static void ReportInFlight(int count)
        {
            try
            {
                StatusBarProgressHost.CoordinatorOrNull?.SetJacketFetchInFlight(Math.Max(0, count));
            }
            catch
            {
                // 起動前や未初期化では無視
            }
        }

        private static async Task<BitmapSource> LoadOnceAsync(
            string url,
            Dispatcher dispatcher,
            CancellationToken cancellationToken)
        {
            // リダイレクト解決は GET 側に任せる（HEAD 専用ハングを避ける）
            byte[] bytes = await DownloadBytesAsync(url, cancellationToken).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            return await dispatcher.InvokeAsync(
                () => CreateBitmapFromBytes(bytes),
                DispatcherPriority.Background);
        }

        private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using HttpResponseMessage response = await Http
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                Uri finalUri = response.RequestMessage?.RequestUri
                    ?? (Uri.TryCreate(url, UriKind.Absolute, out Uri original) ? original : null);
                if (DmmJacketUrls.IsPlaceholderJacketUri(finalUri))
                {
                    return null;
                }

                byte[] bytes = await response.Content
                    .ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);

                return bytes is { Length: > 0 } ? bytes : null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource CreateBitmapFromBytes(byte[] bytes)
        {
            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.StreamSource = stream;
                bitmap.EndInit();

                if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                {
                    return null;
                }

                if (!bitmap.IsFrozen)
                {
                    bitmap.Freeze();
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
