using System.IO;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 現在タブで error サムネイルが表示される状態かを判定する（noFile は含めない）。
    /// </summary>
    internal static class ThumbnailTabErrorDetector
    {
        public static bool IsErrorForTab(MovieRecords item, int tabIndex, ThumbnailLayoutCache cache)
        {
            if (item == null || cache == null || tabIndex < 0 || tabIndex >= cache.TabOutPaths.Length)
            {
                return false;
            }

            string moviePath = item.Movie_Path ?? "";
            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Hash))
            {
                return false;
            }

            string fileBody = Path.GetFileNameWithoutExtension(item.Movie_Name ?? moviePath).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(fileBody))
            {
                return false;
            }

            string expectedThumb = cache.GetExpectedThumbPath(tabIndex, fileBody, item.Hash);
            string errorTemplate = cache.GetErrorPath(tabIndex);
            string noFileTemplate = cache.GetNoFilePath(tabIndex);

            if (!File.Exists(expectedThumb))
            {
                return true;
            }

            if (PlaceholderFilesMatch(expectedThumb, noFileTemplate))
            {
                return false;
            }

            if (PlaceholderFilesMatch(expectedThumb, errorTemplate))
            {
                return true;
            }

            return !ThumbnailValidityHelper.LooksLikeCompositeThumbnail(expectedThumb);
        }

        private static bool PlaceholderFilesMatch(string filePath, string templatePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(templatePath))
            {
                return false;
            }

            if (!File.Exists(filePath) || !File.Exists(templatePath))
            {
                return false;
            }

            FileInfo candidate = new(filePath);
            FileInfo template = new(templatePath);
            if (candidate.Length != template.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> candidateBytes = File.ReadAllBytes(filePath);
            ReadOnlySpan<byte> templateBytes = File.ReadAllBytes(templatePath);
            return candidateBytes.SequenceEqual(templateBytes);
        }
    }
}
