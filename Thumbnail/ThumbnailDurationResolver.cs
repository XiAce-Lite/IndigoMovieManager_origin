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
                using VideoCapture capture = new(movieFullPath);
                capture.Grab();

                if (!capture.IsOpened())
                {
                    durationSec = TryResolveFromShell(movieFullPath);
                    return durationSec > 0;
                }

                double frameCount = capture.Get(VideoCaptureProperties.FrameCount);
                double fps = capture.Get(VideoCaptureProperties.Fps);
                if (fps > 0 && frameCount > 0)
                {
                    durationSec = Math.Truncate(frameCount / fps);
                }

                double durationFromShell = TryResolveFromShell(movieFullPath);
                if (durationFromShell > 0 && durationSec != durationFromShell)
                {
                    durationSec = durationFromShell;
                }

                return durationSec > 0 || durationFromShell > 0;
            }
            catch
            {
                durationSec = TryResolveFromShell(movieFullPath);
                return durationSec > 0;
            }
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
    }
}
