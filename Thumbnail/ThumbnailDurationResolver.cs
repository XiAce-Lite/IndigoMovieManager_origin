using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenCvSharp;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailDurationResolver
    {
        public static bool TryResolve(string movieFullPath, out double durationSec, double knownDurationSec = 0d)
        {
            durationSec = 0;
            if (string.IsNullOrWhiteSpace(movieFullPath) || !Path.Exists(movieFullPath))
            {
                return false;
            }

            if (knownDurationSec > 0d)
            {
                durationSec = Math.Truncate(knownDurationSec);
                return true;
            }

            if (FfmpegPathResolver.TryResolveFfprobe(out string ffprobePath))
            {
                double fromFfprobe = TryResolveFromFfprobe(ffprobePath, movieFullPath);
                if (fromFfprobe > 0d)
                {
                    durationSec = fromFfprobe;
                    return true;
                }
            }

            double fromShell = TryResolveFromShell(movieFullPath);
            if (fromShell > 0d)
            {
                durationSec = fromShell;
                return true;
            }

            double fromOpenCv = TryResolveFromOpenCv(movieFullPath);
            if (fromOpenCv > 0d)
            {
                durationSec = fromOpenCv;
                return true;
            }

            return false;
        }

        /// <summary>
        /// ffprobe → Shell → OpenCV の順で信頼し、OpenCV だけが異常に大きい場合は無視する。
        /// </summary>
        internal static double PickBestDuration(double fromFfprobe, double fromShell, double fromOpenCv)
        {
            if (fromFfprobe > 0d)
            {
                return fromFfprobe;
            }

            if (fromShell > 0d)
            {
                return fromShell;
            }

            if (fromOpenCv > 0d)
            {
                return fromOpenCv;
            }

            return 0d;
        }

        private static double TryResolveFromOpenCv(string movieFullPath)
        {
            try
            {
                using VideoCapture capture = OpenVideoCapture(movieFullPath);
                capture.Grab();

                if (!capture.IsOpened())
                {
                    return 0d;
                }

                double frameCount = capture.Get(VideoCaptureProperties.FrameCount);
                double fps = capture.Get(VideoCaptureProperties.Fps);
                if (fps > 0d && frameCount > 0d)
                {
                    return Math.Truncate(frameCount / fps);
                }
            }
            catch
            {
                // OpenCV 取得失敗時は 0 のまま
            }

            return 0d;
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

        private static double TryResolveFromShell(string movieFullPath)
        {
            try
            {
                FileInfo fi = new(movieFullPath);
                Type shellAppType = Type.GetTypeFromProgID("Shell.Application");
                if (shellAppType == null) { return 0; }

                dynamic shell = Activator.CreateInstance(shellAppType);
                dynamic objFolder = shell.NameSpace(Path.GetDirectoryName(fi.FullName));
                dynamic folderItem = objFolder.ParseName(Path.GetFileName(fi.FullName));
                string timeString = objFolder.GetDetailsOf(folderItem, 27);
                if (TimeSpan.TryParse(timeString, out TimeSpan timeSpan))
                {
                    return timeSpan.TotalSeconds;
                }
            }
            catch
            {
                // Shell32 取得失敗時は 0 のまま
            }

            return 0;
        }

        private static double TryResolveFromFfprobe(string ffprobeExePath, string movieFullPath)
        {
            if (string.IsNullOrWhiteSpace(ffprobeExePath) || !File.Exists(ffprobeExePath))
            {
                return 0;
            }

            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = ffprobeExePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("-v");
                psi.ArgumentList.Add("error");
                psi.ArgumentList.Add("-show_entries");
                psi.ArgumentList.Add("format=duration");
                psi.ArgumentList.Add("-of");
                psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
                psi.ArgumentList.Add(movieFullPath);

                using Process process = Process.Start(psi);
                if (process == null)
                {
                    return 0;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    return 0;
                }

                if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double duration)
                    && duration > 0)
                {
                    return Math.Truncate(duration);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffprobe: {ex.Message}");
            }

            return 0;
        }
    }
}
