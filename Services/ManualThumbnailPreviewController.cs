using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal sealed class ManualThumbnailPreviewController
    {
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _extractCts;
        private CancellationTokenSource _debounceCts;
        private int _previewRequestId;

        public ManualThumbnailPreviewController(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string MoviePath { get; private set; }

        public double PositionMs { get; private set; }

        public double DurationMs { get; private set; }

        public bool IsOpen => !string.IsNullOrWhiteSpace(MoviePath);

        public Action<BitmapSource> OnFrameReady { get; set; }

        public async Task OpenAsync(string moviePath, double startMs)
        {
            CancelPending();
            MoviePath = moviePath;
            DurationMs = 0d;

            double durationSec = await Task.Run(() =>
            {
                return ThumbnailDurationResolver.TryResolve(moviePath, out double resolved)
                    ? resolved
                    : 0d;
            }).ConfigureAwait(true);

            if (durationSec > 0d)
            {
                DurationMs = durationSec * 1000d;
            }

            SetPositionMs(startMs, schedulePreview: false);
        }

        public void Close()
        {
            CancelPending();
            MoviePath = null;
            DurationMs = 0d;
            PositionMs = 0d;
        }

        public void SetPositionMs(double positionMs, bool schedulePreview = true)
        {
            if (DurationMs > 0d)
            {
                PositionMs = Math.Clamp(positionMs, 0d, DurationMs);
            }
            else
            {
                PositionMs = Math.Max(0d, positionMs);
            }

            if (schedulePreview)
            {
                SchedulePreview();
            }
        }

        public int PositionSeconds => (int)(PositionMs / 1000d);

        public string PositionText => TimeSpan.FromMilliseconds(PositionMs).ToString(@"hh\:mm\:ss");

        public void SchedulePreview()
        {
            if (!IsOpen)
            {
                return;
            }

            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            CancellationToken debounceToken = _debounceCts.Token;
            _ = DebounceAndRefreshAsync(debounceToken);
        }

        public Task RefreshPreviewAsync()
        {
            if (!IsOpen)
            {
                return Task.CompletedTask;
            }

            return RefreshPreviewCoreAsync();
        }

        public void CancelPending()
        {
            _debounceCts?.Cancel();
            _extractCts?.Cancel();
        }

        private async Task DebounceAndRefreshAsync(CancellationToken debounceToken)
        {
            try
            {
                await Task.Delay(150, debounceToken).ConfigureAwait(false);
                await RefreshPreviewCoreAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        private async Task RefreshPreviewCoreAsync()
        {
            _extractCts?.Cancel();
            _extractCts = new CancellationTokenSource();
            CancellationToken extractToken = _extractCts.Token;
            int requestId = Interlocked.Increment(ref _previewRequestId);
            string moviePath = MoviePath;
            double positionMs = PositionMs;

            string tempFile = await FfmpegPreviewFrameExtractor
                .TryExtractToTempFileAsync(moviePath, positionMs, extractToken)
                .ConfigureAwait(false);

            if (extractToken.IsCancellationRequested || requestId != _previewRequestId)
            {
                TryDeleteTempFile(tempFile);
                return;
            }

            if (string.IsNullOrWhiteSpace(tempFile))
            {
                return;
            }

            try
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        BitmapSource frame = LoadBitmapSource(tempFile);
                        frame?.Freeze();
                        OnFrameReady?.Invoke(frame);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [preview] load: {ex.Message}"
                        );
                    }
                });
            }
            finally
            {
                TryDeleteTempFile(tempFile);
            }
        }

        private static BitmapSource LoadBitmapSource(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            return image;
        }

        private static void TryDeleteTempFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }
}
