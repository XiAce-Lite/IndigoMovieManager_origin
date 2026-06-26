using System.Diagnostics;
using System.IO;
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

        public static bool IsAvailable => RequiredFiles.All(name =>
            File.Exists(Path.Combine(AppContext.BaseDirectory, name)));

        public static bool TryFetch(string moviePath, out SinkuMetadata metadata)
        {
            metadata = null;

            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return false;
            }

            string sinkuExe = ResolveSinkuExePath();
            if (!File.Exists(sinkuExe))
            {
                return false;
            }

            try
            {
                using Process process = new();
                process.StartInfo.FileName = sinkuExe;
                process.StartInfo.Arguments = $"\"{moviePath}\"";
                process.StartInfo.WorkingDirectory = AppContext.BaseDirectory;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;

                process.Start();
                process.WaitForExit();

                string output = process.StandardOutput.ReadToEnd();
                if (string.IsNullOrWhiteSpace(output))
                {
                    return false;
                }

                XDocument doc = XDocument.Parse(output);
                IEnumerable<XElement> infos = from item in doc.Elements("fields") select item;
                foreach (XElement info in infos)
                {
                    long movieLengthSec = 0;
                    string lengthText = info.Element("movie_length")?.Value;
                    if (!string.IsNullOrEmpty(lengthText))
                    {
                        _ = long.TryParse(lengthText, out movieLengthSec);
                    }

                    metadata = new SinkuMetadata(
                        info.Element("container")?.Value ?? "",
                        info.Element("video")?.Value ?? "",
                        info.Element("audio")?.Value ?? "",
                        info.Element("extra")?.Value ?? "",
                        movieLengthSec);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static string ResolveSinkuExePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "sinku.exe");
        }
    }
}
