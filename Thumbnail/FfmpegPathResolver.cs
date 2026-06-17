using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegPathResolver
    {
        private const string ExePathEnvName = "IMM_FFMPEG_EXE_PATH";
        private const string ForceFfmpegEnvName = "IMM_FORCE_FFMPEG";

        public static bool IsForceFfmpegEnabled()
        {
            string mode = Environment.GetEnvironmentVariable(ForceFfmpegEnvName)?.Trim() ?? "";
            return mode is "1" or "true" or "on" or "yes";
        }

        public static bool IsFallbackEnabled()
        {
            if (!Properties.Settings.Default.EnableFfmpegFallback)
            {
                return false;
            }

            return TryResolve(out _);
        }

        public static bool TryResolve(out string ffmpegExePath)
        {
            ffmpegExePath = "";

            string configured = Environment.GetEnvironmentVariable(ExePathEnvName)?.Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = Properties.Settings.Default.FfmpegExePath?.Trim();
            }

            if (!string.IsNullOrWhiteSpace(configured))
            {
                string normalized = configured.Trim('"');
                if (File.Exists(normalized))
                {
                    ffmpegExePath = Path.GetFullPath(normalized);
                    return true;
                }

                if (Directory.Exists(normalized))
                {
                    string candidate = Path.Combine(normalized, "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        ffmpegExePath = candidate;
                        return true;
                    }
                }
            }

            string baseDir = AppContext.BaseDirectory;
            string[] bundledCandidates =
            [
                Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
                Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe"),
                Path.Combine(baseDir, "ffmpeg.exe"),
            ];

            foreach (string candidate in bundledCandidates)
            {
                if (File.Exists(candidate))
                {
                    ffmpegExePath = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
