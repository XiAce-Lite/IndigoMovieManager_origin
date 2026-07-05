using System.IO;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 現在タブで error サムネイルが表示される状態かを判定する（noFile は含めない）。
    /// </summary>
    internal static class ThumbnailTabErrorDetector
    {
        public static bool IsErrorForEngine(
            MovieRecords item,
            SkinEngine engine,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext hashSyncContext = null)
        {
            if (item == null || cache == null)
            {
                return false;
            }

            ThumbnailLayoutSpec spec = ThumbnailLayoutResolver.GetActiveListLayout(engine);
            return IsErrorForLayout(item, spec, cache, hashSyncContext);
        }

        public static bool IsErrorForLayout(
            MovieRecords item,
            ThumbnailLayoutSpec spec,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext hashSyncContext = null)
        {
            if (item == null || spec == null || cache == null)
            {
                return false;
            }

            string hash = ThumbnailHashSync.ResolveHashForThumbnail(
                item,
                spec,
                cache,
                hashSyncContext,
                ThumbnailHashSync.ThumbPathSatisfactionMode.ErrorCheck);

            return IsErrorThumbnailState(
                item,
                cache.GetExpectedThumbPath(spec, GetMovieBody(item), hash),
                cache.GetErrorPath(2),
                cache.GetNoFilePath(2));
        }

        public static bool IsDetailThumbnailError(
            MovieRecords item,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext hashSyncContext = null)
        {
            if (item == null || cache == null)
            {
                return false;
            }

            string hash = ThumbnailHashSync.ResolveHashForThumbnail(
                item,
                ThumbnailLayoutSpec.DetailPaneLayout,
                cache,
                hashSyncContext,
                ThumbnailHashSync.ThumbPathSatisfactionMode.ErrorCheck);

            return IsErrorThumbnailState(
                item,
                cache.GetExpectedDetailThumbPath(GetMovieBody(item), hash),
                cache.GetErrorPath(2),
                cache.GetNoFilePath(2));
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

        private static string GetMovieBody(MovieRecords item) =>
            ThumbnailMovieNaming.GetMovieBody(item);

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
