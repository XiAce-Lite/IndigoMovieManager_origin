using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace IndigoMovieManager
{
    internal sealed record SinkuMetadata(
        string Container,
        string Video,
        string Audio,
        string Extra,
        long MovieLengthSec);

    /// <summary>
    /// sinku.exe 経由で動画のフォーマット・コーデック情報を取得する。
    /// </summary>
    internal static class SinkuMetadataFetcher
    {
        private static readonly string[] RequiredFiles = ["sinku.exe", "Sinku.dll", "format.ini", "codecs.ini"];
        private static readonly object FetchGate = new();

        public static bool IsAvailable => RequiredFiles.All(name =>
            File.Exists(Path.Combine(AppContext.BaseDirectory, name)));

        public static bool TryFetch(string moviePath, out SinkuMetadata metadata)
        {
            metadata = null;

            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return false;
            }

            if (!IsAvailable)
            {
                return false;
            }

            string sinkuExe = ResolveSinkuExePath();
            if (!File.Exists(sinkuExe))
            {
                return false;
            }

            // sinku / Sinku.dll は同時実行に弱いため直列化する
            lock (FetchGate)
            {
                return TryFetchCore(sinkuExe, moviePath, out metadata);
            }
        }

        private static bool TryFetchCore(string sinkuExe, string moviePath, out SinkuMetadata metadata)
        {
            metadata = null;
            try
            {
                using Process process = new();
                process.StartInfo.FileName = sinkuExe;
                process.StartInfo.Arguments = $"\"{moviePath}\"";
                process.StartInfo.WorkingDirectory = AppContext.BaseDirectory;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        stdout.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        stderr.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                if (!process.WaitForExit(120_000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore
                    }

                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [sinku] timeout: {moviePath}");
                    return false;
                }

                // 非同期読取の完了を待つ
                process.WaitForExit();

                string output = stdout.ToString();
                if (string.IsNullOrWhiteSpace(output))
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [sinku] empty stdout (exit={process.ExitCode}): {moviePath}"
                        + (stderr.Length > 0 ? $" stderr={stderr}" : ""));
                    return false;
                }

                XDocument doc = XDocument.Parse(output);
                XElement fields = doc.Root?.Name.LocalName == "fields"
                    ? doc.Root
                    : doc.Root?.Element("fields") ?? doc.Element("fields");
                if (fields == null)
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [sinku] no <fields>: {moviePath}");
                    return false;
                }

                long movieLengthSec = 0;
                string lengthText = fields.Element("movie_length")?.Value;
                if (!string.IsNullOrEmpty(lengthText))
                {
                    _ = long.TryParse(lengthText, out movieLengthSec);
                }

                metadata = new SinkuMetadata(
                    fields.Element("container")?.Value ?? "",
                    fields.Element("video")?.Value ?? "",
                    fields.Element("audio")?.Value ?? "",
                    fields.Element("extra")?.Value ?? "",
                    movieLengthSec);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [sinku] failed: {moviePath} : {ex.Message}");
                return false;
            }
        }

        private static string ResolveSinkuExePath() =>
            Path.Combine(AppContext.BaseDirectory, "sinku.exe");
    }
}
