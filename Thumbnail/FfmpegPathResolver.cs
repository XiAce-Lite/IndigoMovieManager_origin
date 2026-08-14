using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegPathResolver
    {
        private const string ExePathEnvName = "IMM_FFMPEG_EXE_PATH";
        private const string ForceFfmpegEnvName = "IMM_FORCE_FFMPEG";
        private const string ThumbEngineEnvName = "IMM_THUMB_ENGINE";
        private const string AutoEngineEnvName = "IMM_THUMB_AUTO_ENGINE";

        /// <summary>ベンチ結果: DivCount がこの値以下の自動サムネのみ coarse seek を OpenCV より先に試す。</summary>
        internal const int AutoCoarseSeekMaxDivCount = 4;

        public static string GetThumbEngineMode()
        {
            return Environment.GetEnvironmentVariable(ThumbEngineEnvName)?.Trim() ?? "";
        }

        public static bool IsOnePassEngineRequested()
        {
            string mode = GetThumbEngineMode();
            return string.Equals(mode, "ffmpeg1pass", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsForceFfmpegEnabled()
        {
            string mode = Environment.GetEnvironmentVariable(ForceFfmpegEnvName)?.Trim() ?? "";
            return mode is "1" or "true" or "on" or "yes";
        }

        /// <summary>
        /// 自動サムネで粗い独立 seek（FFmpeg）を OpenCV より先に試すか。
        /// <paramref name="divCount"/> が <see cref="AutoCoarseSeekMaxDivCount"/> 以下のときのみ true。
        /// <c>IMM_THUMB_AUTO_ENGINE=opencv</c> で従来どおり OpenCV 優先に戻せる。
        /// </summary>
        public static bool IsAutoCoarseSeekPreferred(int divCount)
        {
            string mode = Environment.GetEnvironmentVariable(AutoEngineEnvName)?.Trim() ?? "";
            if (mode is "opencv" or "0" or "off" or "false" or "no")
            {
                return false;
            }

            if (divCount < 1 || divCount > AutoCoarseSeekMaxDivCount)
            {
                return false;
            }

            return IsFallbackEnabled();
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

            if (TryResolveFromPathEnvironment("ffmpeg.exe", out string fromPath))
            {
                ffmpegExePath = fromPath;
                return true;
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

            if (TryResolveFromPathEnvironment("ffprobe.exe", out string fromPath))
            {
                ffprobeExePath = fromPath;
                return true;
            }

            return false;
        }

        private static bool TryResolveFromPathEnvironment(string fileName, out string resolvedPath)
        {
            resolvedPath = "";
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = dir.Trim().Trim('"');
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                try
                {
                    string candidate = Path.Combine(trimmed, fileName);
                    if (File.Exists(candidate))
                    {
                        resolvedPath = Path.GetFullPath(candidate);
                        return true;
                    }
                }
                catch (ArgumentException)
                {
                    // PATH 内の不正なディレクトリは無視
                }
                catch (IOException)
                {
                    // PATH 内の不正なディレクトリは無視
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
