using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace IndigoMovieManager.Thumbnail
{
    internal static class OpenCvPreviewFrameExtractor
    {
        public static Task<string> TryExtractToTempFileAsync(
            string movieFullPath,
            double positionMs,
            CancellationToken cts)
        {
            return Task.Run(() => TryExtractToTempFile(movieFullPath, positionMs, cts), cts);
        }

        private static string TryExtractToTempFile(
            string movieFullPath,
            double positionMs,
            CancellationToken cts)
        {
            if (string.IsNullOrWhiteSpace(movieFullPath) || !File.Exists(movieFullPath))
            {
                return null;
            }

            cts.ThrowIfCancellationRequested();

            string tempFile = Path.Combine(
                Path.GetTempPath(),
                $"imm_preview_{Guid.NewGuid():N}.jpg");

            using VideoCapture capture = OpenVideoCapture(movieFullPath);
            if (!capture.IsOpened())
            {
                return null;
            }

            capture.Grab();
            capture.PosMsec = (int)Math.Max(0d, positionMs);

            using Mat img = new();
            int attempts = 0;
            while (!capture.Read(img))
            {
                capture.PosMsec += 100;
                attempts++;
                if (attempts > 100)
                {
                    return null;
                }

                cts.ThrowIfCancellationRequested();
            }

            if (img.Empty() || img.Width == 0 || img.Height == 0)
            {
                return null;
            }

            BitmapConverter.ToBitmap(img).Save(tempFile, ImageFormat.Jpeg);
            return File.Exists(tempFile) ? tempFile : null;
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
    }
}
