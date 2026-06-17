using System.IO;
using System.Security.Cryptography;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailDuplicateDetector
    {
        private const int SampleBytes = 32 * 1024;

        public static bool HasDuplicatePanels(IReadOnlyList<string> panelPaths)
        {
            if (panelPaths == null || panelPaths.Count < 2) { return false; }

            HashSet<string> signatures = new(StringComparer.Ordinal);
            foreach (string path in panelPaths)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return false;
                }

                string signature = BuildQuickSignature(path);
                if (!signatures.Add(signature))
                {
                    continue;
                }
            }

            return signatures.Count <= 1;
        }

        private static string BuildQuickSignature(string path)
        {
            try
            {
                FileInfo info = new(path);
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                int readSize = (int)Math.Min(SampleBytes, stream.Length);
                byte[] buffer = new byte[readSize];
                int read = stream.Read(buffer, 0, readSize);
                if (read < 1)
                {
                    return $"{info.Length}:empty";
                }

                byte[] hash = SHA256.HashData(buffer.AsSpan(0, read));
                return $"{info.Length}:{Convert.ToHexString(hash)}";
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }
    }
}
