using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IndigoMovieManager.Services;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// プレビュー再生用に VideoCapture を開きっぱなしにし、シーク＋フレーム読み出しを高速化する。
    /// </summary>
    internal sealed class OpenCvPreviewSession : IDisposable
    {
        private readonly object _sync = new();
        private VideoCapture _capture;
        private bool _disposed;

        public double Fps { get; private set; } = PreviewPlaybackTiming.DefaultFps;

        public bool TryOpen(string movieFullPath)
        {
            DisposeCapture();

            if (string.IsNullOrWhiteSpace(movieFullPath) || !File.Exists(movieFullPath))
            {
                return false;
            }

            VideoCapture capture = OpenVideoCapture(movieFullPath);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                return false;
            }

            capture.Grab();
            double fps = capture.Get(VideoCaptureProperties.Fps);
            Fps = PreviewPlaybackTiming.NormalizeFps(fps);

            _capture = capture;
            return true;
        }

        public BitmapSource TryReadFrame(double positionMs)
        {
            if (_disposed)
            {
                return null;
            }

            lock (_sync)
            {
                if (_capture == null || !_capture.IsOpened())
                {
                    return null;
                }

                _capture.PosMsec = (int)Math.Max(0d, positionMs);

                using Mat mat = new();
                int attempts = 0;
                while (!_capture.Read(mat))
                {
                    _capture.PosMsec += 33;
                    attempts++;
                    if (attempts > 30)
                    {
                        return null;
                    }
                }

                if (mat.Empty() || mat.Width == 0 || mat.Height == 0)
                {
                    return null;
                }

                using Bitmap bitmap = BitmapConverter.ToBitmap(mat);
                return CreateBitmapSource(bitmap);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeCapture();
        }

        private void DisposeCapture()
        {
            lock (_sync)
            {
                if (_capture == null)
                {
                    return;
                }

                _capture.Dispose();
                _capture = null;
            }
        }

        private static BitmapSource CreateBitmapSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private static VideoCapture OpenVideoCapture(string movieFullPath)
        {
            VideoCapture ffmpegCapture = new(movieFullPath, VideoCaptureAPIs.FFMPEG);
            if (ffmpegCapture.IsOpened())
            {
                return ffmpegCapture;
            }

            ffmpegCapture.Dispose();
            return new VideoCapture(movieFullPath);
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
