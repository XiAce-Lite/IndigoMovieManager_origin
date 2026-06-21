using System.Diagnostics;
using System.Globalization;
using System.IO;
using OpenCvSharp;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailDurationResolver
    {
        public static bool TryResolve(string movieFullPath, out double durationSec)
        {
            durationSec = 0;
            if (string.IsNullOrWhiteSpace(movieFullPath) || !Path.Exists(movieFullPath))
            {
                return false;
            }

            try
            {
                using VideoCapture capture = OpenVideoCapture(movieFullPath);
                capture.Grab();

                if (capture.IsOpened())
                {
                    double frameCount = capture.Get(VideoCaptureProperties.FrameCount);
                    double fps = capture.Get(VideoCaptureProperties.Fps);
                    if (fps > 0 && frameCount > 0)
                    {
                        durationSec = Math.Truncate(frameCount / fps);
                    }
                }
            }
            catch
            {
                // OpenCV 取得失敗時は後段へ
            }

            double durationFromShell = TryResolveFromShell(movieFullPath);
            if (durationFromShell > 0)
            {
                durationSec = durationFromShell;
            }

            if (durationSec <= 0 && FfmpegPathResolver.TryResolveFfprobe(out string ffprobePath))
            {
                double durationFromFfprobe = TryResolveFromFfprobe(ffprobePath, movieFullPath);
                if (durationFromFfprobe > 0)
                {
                    durationSec = durationFromFfprobe;
                }
            }

            return durationSec > 0;
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
