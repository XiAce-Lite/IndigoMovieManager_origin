using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ZipMediaKind
    {
        public static bool IsZipPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsZipRecord(MovieRecords record)
        {
            if (record == null)
            {
                return false;
            }

            if (string.Equals(record.Container, "zip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsZipPath(record.Movie_Path);
        }
    }
}
