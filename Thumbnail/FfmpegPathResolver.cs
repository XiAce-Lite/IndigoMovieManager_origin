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

            foreach (string candidate in GetBundledToolCandidates("ffmpeg.exe"))
            {
                if (File.Exists(candidate))
                {
                    ffmpegExePath = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveFfprobe(out string ffprobeExePath)
        {
            ffprobeExePath = "";

            if (TryResolve(out string ffmpegExePath))
            {
                string sibling = Path.Combine(Path.GetDirectoryName(ffmpegExePath) ?? "", "ffprobe.exe");
                if (File.Exists(sibling))
                {
                    ffprobeExePath = sibling;
                    return true;
                }
            }

            foreach (string candidate in GetBundledToolCandidates("ffprobe.exe"))
            {
                if (File.Exists(candidate))
                {
                    ffprobeExePath = candidate;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> GetBundledToolCandidates(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            return
            [
                Path.Combine(baseDir, "ffmpeg", fileName),
                Path.Combine(baseDir, "tools", "ffmpeg", fileName),
                Path.Combine(baseDir, "tools", fileName),
                Path.Combine(baseDir, fileName),
            ];
        }
    }
}
