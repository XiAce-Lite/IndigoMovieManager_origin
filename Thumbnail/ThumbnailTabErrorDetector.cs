using System.IO;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 現在タブで error サムネイルが表示される状態かを判定する（noFile は含めない）。
    /// </summary>
    internal static class ThumbnailTabErrorDetector
    {
        public static bool IsErrorForEngine(MovieRecords item, SkinEngine engine, ThumbnailLayoutCache cache)
        {
            if (item == null || cache == null)
            {
                return false;
            }

            ThumbnailLayoutSpec spec = ThumbnailLayoutResolver.GetActiveListLayout(engine);
            return IsErrorForLayout(item, spec, cache);
        }

        public static bool IsErrorForLayout(MovieRecords item, ThumbnailLayoutSpec spec, ThumbnailLayoutCache cache)
        {
            if (item == null || spec == null || cache == null)
            {
                return false;
            }

            return IsErrorThumbnailState(
                item,
                cache.GetExpectedThumbPath(spec, GetMovieBody(item), item.Hash),
                cache.GetErrorPath(2),
                cache.GetNoFilePath(2));
        }

        public static bool IsErrorForTab(MovieRecords item, int tabIndex, ThumbnailLayoutCache cache)
        {
            if (item == null || cache == null)
            {
                return false;
            }

            if (tabIndex == 99)
            {
                return IsDetailThumbnailError(item, cache);
            }

            if (tabIndex < 0 || tabIndex >= cache.TabOutPaths.Length)
            {
                return false;
            }

            return IsErrorThumbnailState(
                item,
                cache.GetExpectedThumbPath(tabIndex, GetMovieBody(item), item.Hash),
                cache.GetErrorPath(tabIndex),
                cache.GetNoFilePath(tabIndex));
        }

        public static bool IsDetailThumbnailError(MovieRecords item, ThumbnailLayoutCache cache)
        {
            if (item == null || cache == null)
            {
                return false;
            }

            return IsErrorThumbnailState(
                item,
                cache.GetExpectedThumbPath(99, GetMovieBody(item), item.Hash),
                cache.GetErrorPath(99),
                cache.GetNoFilePath(99));
        }

        private static bool IsErrorThumbnailState(
            MovieRecords item,
            string expectedThumb,
            string errorTemplate,
            string noFileTemplate)
        {
            string moviePath = item.Movie_Path ?? "";
            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Hash))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedThumb))
            {
                return false;
            }

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

        private static string GetMovieBody(MovieRecords item)
        {
            string moviePath = item.Movie_Path ?? "";
            return Path.GetFileNameWithoutExtension(item.Movie_Name ?? moviePath).ToLowerInvariant();
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
