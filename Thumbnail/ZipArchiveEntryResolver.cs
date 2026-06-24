using System.IO.Compression;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ZipArchiveEntryResolver
    {
        public static ZipArchiveEntry FindEntry(ZipArchive archive, string entryName)
        {
            if (archive == null || string.IsNullOrWhiteSpace(entryName))
            {
                return null;
            }

            ZipArchiveEntry entry = archive.GetEntry(entryName);
            if (entry != null)
            {
                return entry;
            }

            string normalized = entryName.Replace('\\', '/');
            entry = archive.GetEntry(normalized);
            if (entry != null)
            {
                return entry;
            }

            foreach (ZipArchiveEntry candidate in archive.Entries)
            {
                if (string.IsNullOrEmpty(candidate.Name))
                {
                    continue;
                }

                if (string.Equals(candidate.FullName, entryName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.FullName, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
