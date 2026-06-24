using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenCvSharp;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// ZIP 内 WebP をパネル JPEG に変換する。実ファイルは OpenCV だけでは読めないことが多いため ffmpeg を優先する。
    /// </summary>
    internal static class ZipWebpDecoder
    {
        public static bool TryRenderToLetterboxedJpeg(
            byte[] webpData,
            string destPath,
            int panelWidth,
            int panelHeight,
            CancellationToken cts = default)
        {
            if (webpData == null || webpData.Length == 0 || string.IsNullOrWhiteSpace(destPath))
            {
                return false;
            }

            if (TryRenderWithFfmpeg(webpData, destPath, panelWidth, panelHeight, cts))
            {
                return true;
            }

            if (TryRenderWithOpenCv(webpData, destPath, panelWidth, panelHeight))
            {
                return true;
            }

            Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] zip webp: decode failed");
            return false;
        }

        private static bool TryRenderWithFfmpeg(
            byte[] webpData,
            string destPath,
            int panelWidth,
            int panelHeight,
            CancellationToken cts)
        {
            if (!FfmpegPathResolver.TryResolve(out string ffmpegExePath))
            {
                return false;
            }

            string tempDir = Path.GetDirectoryName(destPath) ?? Path.GetTempPath();
            string inputPath = Path.Combine(tempDir, $"imm_webp_{Guid.NewGuid():N}.webp");
            try
            {
                File.WriteAllBytes(inputPath, webpData);

                string destDirectory = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrWhiteSpace(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                string vf = string.Create(
                    CultureInfo.InvariantCulture,
                    $"scale={panelWidth}:{panelHeight}:force_original_aspect_ratio=decrease,pad={panelWidth}:{panelHeight}:(ow-iw)/2:(oh-ih)/2:color=black");

                List<string> args =
                [
                    "-y",
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-i",
                    inputPath,
                    "-frames:v",
                    "1",
                    "-vf",
                    vf,
                    "-q:v",
                    "2",
                    destPath,
                ];

                (bool ok, string stderr) = FfmpegProcessRunner
                    .RunAsync(ffmpegExePath, args, TimeSpan.FromSeconds(45), cts)
                    .GetAwaiter()
                    .GetResult();

                if (!ok)
                {
                    Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] zip webp ffmpeg: {stderr}");
                }

                return ok && File.Exists(destPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] zip webp ffmpeg: {ex.Message}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(inputPath))
                    {
                        File.Delete(inputPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static bool TryRenderWithOpenCv(byte[] webpData, string destPath, int panelWidth, int panelHeight)
        {
            try
            {
                using Mat source = Cv2.ImDecode(webpData, ImreadModes.Color);
                if (source.Empty())
                {
                    return false;
                }

                return RenderMatToLetterboxedJpeg(source, destPath, panelWidth, panelHeight);
            }
            catch
            {
                return false;
            }
        }

        internal static bool RenderMatToLetterboxedJpeg(Mat source, string destPath, int panelWidth, int panelHeight)
        {
            ComputeLetterboxDrawSize(
                source.Width,
                source.Height,
                panelWidth,
                panelHeight,
                out int drawWidth,
                out int drawHeight,
                out int drawX,
                out int drawY);

            using Mat resized = new();
            Cv2.Resize(source, resized, new OpenCvSharp.Size(drawWidth, drawHeight));
            using Mat panel = new(panelHeight, panelWidth, MatType.CV_8UC3, Scalar.Black);
            using Mat roi = panel[new Rect(drawX, drawY, drawWidth, drawHeight)];
            resized.CopyTo(roi);

            return Cv2.ImWrite(destPath, panel);
        }

        private static void ComputeLetterboxDrawSize(
            int sourceWidth,
            int sourceHeight,
            int panelWidth,
            int panelHeight,
            out int drawWidth,
            out int drawHeight,
            out int drawX,
            out int drawY)
        {
            double scaleX = (double)panelWidth / sourceWidth;
            double scaleY = (double)panelHeight / sourceHeight;
            double scale = Math.Min(scaleX, scaleY);

            drawWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            drawHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            drawX = (panelWidth - drawWidth) / 2;
            drawY = (panelHeight - drawHeight) / 2;
        }
    }
}
