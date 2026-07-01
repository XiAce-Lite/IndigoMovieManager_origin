using System.Diagnostics;
using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegHardwareDecodePolicy
    {
        private static readonly FfmpegHardwareDecodeMode[] AutoAttemptOrder =
        [
            FfmpegHardwareDecodeMode.Cuda,
            FfmpegHardwareDecodeMode.Qsv,
            FfmpegHardwareDecodeMode.D3d11va,
            FfmpegHardwareDecodeMode.Dxva2,
        ];

        private const string HwDecodeEnvName = "IMM_FFMPEG_HW_DECODE";

        private static readonly object Sync = new();
        private static string _cachedFfmpegPath = "";
        private static HashSet<string> _availableHwaccels = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<FfmpegHardwareDecodeMode> _failedModes = [];
        private static bool _probed;

        public static FfmpegHardwareDecodeMode GetConfiguredMode()
        {
            string env = Environment.GetEnvironmentVariable(HwDecodeEnvName)?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(env)
                && Enum.TryParse(env, ignoreCase: true, out FfmpegHardwareDecodeMode envMode))
            {
                return envMode;
            }

            string raw = Properties.Settings.Default.FfmpegHardwareDecodeMode?.Trim() ?? "";
            return Enum.TryParse(raw, ignoreCase: true, out FfmpegHardwareDecodeMode mode)
                ? mode
                : FfmpegHardwareDecodeMode.Off;
        }

        public static bool IsHardwareDecodeConfigured()
        {
            return GetConfiguredMode() != FfmpegHardwareDecodeMode.Off;
        }

        public static IReadOnlyList<FfmpegHardwareDecodeMode> GetModesToAttempt(string ffmpegExePath)
        {
            FfmpegHardwareDecodeMode configured = GetConfiguredMode();
            if (configured == FfmpegHardwareDecodeMode.Off)
            {
                return Array.Empty<FfmpegHardwareDecodeMode>();
            }

            if (string.IsNullOrWhiteSpace(ffmpegExePath) || !File.Exists(ffmpegExePath))
            {
                return Array.Empty<FfmpegHardwareDecodeMode>();
            }

            EnsureHwaccelsProbed(ffmpegExePath);

            if (configured == FfmpegHardwareDecodeMode.Auto)
            {
                return FilterAvailableModes(AutoAttemptOrder);
            }

            return FilterAvailableModes([configured]);
        }

        public static string GetHwaccelName(FfmpegHardwareDecodeMode mode)
        {
            return mode switch
            {
                FfmpegHardwareDecodeMode.Cuda => "cuda",
                FfmpegHardwareDecodeMode.Qsv => "qsv",
                FfmpegHardwareDecodeMode.D3d11va => "d3d11va",
                FfmpegHardwareDecodeMode.Dxva2 => "dxva2",
                _ => "",
            };
        }

        public static void MarkModeFailed(FfmpegHardwareDecodeMode mode)
        {
            lock (Sync)
            {
                _failedModes.Add(mode);
            }
        }

        public static void InvalidateCache()
        {
            lock (Sync)
            {
                _cachedFfmpegPath = "";
                _availableHwaccels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _failedModes = [];
                _probed = false;
            }
        }

        internal static IReadOnlyList<string> ParseHwaccelsOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return Array.Empty<string>();
            }

            List<string> hwaccels = [];
            foreach (string rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("Hardware acceleration methods", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hwaccels.Add(line);
            }

            return hwaccels;
        }

        private static void EnsureHwaccelsProbed(string ffmpegExePath)
        {
            string normalizedPath = Path.GetFullPath(ffmpegExePath);
            lock (Sync)
            {
                if (_probed && string.Equals(_cachedFfmpegPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            HashSet<string> probed = ProbeAvailableHwaccels(normalizedPath);
            lock (Sync)
            {
                _cachedFfmpegPath = normalizedPath;
                _availableHwaccels = probed;
                _probed = true;
            }
        }

        private static HashSet<string> ProbeAvailableHwaccels(string ffmpegExePath)
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = ffmpegExePath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add("-hide_banner");
                psi.ArgumentList.Add("-hwaccels");

                using Process process = Process.Start(psi);
                if (process == null)
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                string stdout = process.StandardOutput.ReadToEnd();
                _ = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [ffmpeg] hwaccels probe failed exit={process.ExitCode}"
                    );
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                return ParseHwaccelsOutput(stdout)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [ffmpeg] hwaccels probe: {ex.Message}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static IReadOnlyList<FfmpegHardwareDecodeMode> FilterAvailableModes(
            IReadOnlyList<FfmpegHardwareDecodeMode> candidates)
        {
            List<FfmpegHardwareDecodeMode> available = [];
            lock (Sync)
            {
                foreach (FfmpegHardwareDecodeMode mode in candidates)
                {
                    if (_failedModes.Contains(mode))
                    {
                        continue;
                    }

                    string hwaccel = GetHwaccelName(mode);
                    if (!string.IsNullOrWhiteSpace(hwaccel) && _availableHwaccels.Contains(hwaccel))
                    {
                        available.Add(mode);
                    }
                }
            }

            return available;
        }
    }
}
