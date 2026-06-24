using System.IO;

namespace IndigoMovieManager.Services
{
    internal static class MediaPathNormalizer
    {
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            try
            {
                string fullPath = Path.GetFullPath(path.Trim());
                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
