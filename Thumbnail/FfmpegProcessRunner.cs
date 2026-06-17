using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegProcessRunner
    {
        public static async Task<(bool ok, string stderr)> RunAsync(
            string ffmpegExePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cts
        )
        {
            if (string.IsNullOrWhiteSpace(ffmpegExePath) || !File.Exists(ffmpegExePath))
            {
                return (false, "ffmpeg executable not found");
            }

            (bool ok, string stderr) result = await RunProcessInternalAsync(
                ffmpegExePath,
                arguments,
                timeout,
                cts
            ).ConfigureAwait(false);

            if (result.ok)
            {
                return result;
            }

            if (!TryGetShortPath(ffmpegExePath, out string shortExePath))
            {
                return result;
            }

            List<string> shortArgs = [.. arguments];
            for (int i = 0; i < shortArgs.Count; i++)
            {
                if (shortArgs[i].Contains(' ') && File.Exists(shortArgs[i]))
                {
                    if (TryGetShortPath(shortArgs[i], out string shortArgPath))
                    {
                        shortArgs[i] = shortArgPath;
                    }
                }
            }

            return await RunProcessInternalAsync(shortExePath, shortArgs, timeout, cts)
                .ConfigureAwait(false);
        }

        private static async Task<(bool ok, string stderr)> RunProcessInternalAsync(
            string ffmpegExePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cts
        )
        {
            ProcessStartInfo psi = new()
            {
                FileName = ffmpegExePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            foreach (string arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            using Process process = new() { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
            {
                return (false, "process start returned false");
            }

            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch
            {
                // 優先度変更失敗は無視
            }

            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cts);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cts);

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cts);
            Task delayTask = Task.Delay(timeout, timeoutCts.Token);
            Task waitTask = process.WaitForExitAsync(cts);
            Task completed = await Task.WhenAny(waitTask, delayTask).ConfigureAwait(false);

            if (completed == delayTask)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // ignore
                }

                return (false, $"ffmpeg timeout ({timeout.TotalSeconds:0}s)");
            }

            timeoutCts.Cancel();
            await waitTask.ConfigureAwait(false);

            string stderr = await stderrTask.ConfigureAwait(false);
            _ = await stdoutTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return (false, string.IsNullOrWhiteSpace(stderr) ? $"exit={process.ExitCode}" : stderr);
            }

            return (true, stderr);
        }

        private static bool TryGetShortPath(string longPath, out string shortPath)
        {
            shortPath = "";
            if (string.IsNullOrWhiteSpace(longPath))
            {
                return false;
            }

            StringBuilder buffer = new(512);
            int length = GetShortPathName(longPath, buffer, buffer.Capacity);
            if (length <= 0 || length >= buffer.Capacity)
            {
                return false;
            }

            shortPath = buffer.ToString();
            return !string.IsNullOrWhiteSpace(shortPath);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);
    }
}
