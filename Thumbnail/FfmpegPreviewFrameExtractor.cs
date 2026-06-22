using System.Globalization;
using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegPreviewFrameExtractor
    {
        public static async Task<string> TryExtractToTempFileAsync(
            string movieFullPath,
            double positionMs,
            CancellationToken cts
        )
        {
            if (string.IsNullOrWhiteSpace(movieFullPath) || !File.Exists(movieFullPath))
            {
                return null;
            }

            if (!FfmpegPathResolver.TryResolve(out string ffmpegExePath))
            {
                return null;
            }

            string tempFile = Path.Combine(
                Path.GetTempPath(),
                $"imm_preview_{Guid.NewGuid():N}.jpg"
            );
            double seekSec = Math.Max(0d, positionMs / 1000d);
            string seekText = seekSec.ToString("0.###", CultureInfo.InvariantCulture);

            List<string> args =
            [
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-an",
                "-sn",
                "-dn",
                "-ss",
                seekText,
                "-i",
                movieFullPath,
                "-frames:v",
                "1",
                "-pix_fmt",
                "yuv420p",
                "-q:v",
                "2",
                tempFile,
            ];

            (bool ok, string stderr) = await FfmpegProcessRunner
                .RunAsync(ffmpegExePath, args, TimeSpan.FromSeconds(30), cts)
                .ConfigureAwait(false);

            if (!ok || !File.Exists(tempFile))
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* ignore */ }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [preview] ffmpeg: {stderr}"
                );
                return null;
            }

            return tempFile;
        }
    }
}
