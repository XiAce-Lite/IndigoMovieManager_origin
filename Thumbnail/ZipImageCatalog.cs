using System.IO;
using System.IO.Compression;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ZipImageCatalog
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico",
        };

        public static bool TryGetImageEntries(string zipPath, out IReadOnlyList<string> entryNames)
        {
            entryNames = [];
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
            {
                return false;
            }

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(zipPath);
                List<string> names = [];
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    if (!IsImageEntry(entry.FullName))
                    {
                        continue;
                    }

                    names.Add(entry.FullName);
                }

                names.Sort(StringComparer.OrdinalIgnoreCase);
                entryNames = names;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryExtractEntry(string zipPath, int imageIndex, string destPath)
        {
            if (!TryGetImageEntries(zipPath, out IReadOnlyList<string> entries)
                || imageIndex < 0
                || imageIndex >= entries.Count)
            {
                return false;
            }

            try
            {
                string entryName = entries[imageIndex];
                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                using ZipArchive archive = ZipFile.OpenRead(zipPath);
                ZipArchiveEntry entry = ZipArchiveEntryResolver.FindEntry(archive, entryName);
                if (entry == null)
                {
                    return false;
                }

                entry.ExtractToFile(destPath, overwrite: true);
                return File.Exists(destPath);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWebpEntry(string entryFullName)
        {
            return !string.IsNullOrWhiteSpace(entryFullName)
                && entryFullName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImageEntry(string entryFullName)
        {
            string ext = Path.GetExtension(entryFullName);
            return !string.IsNullOrEmpty(ext) && ImageExtensions.Contains(ext);
        }
    }
}
