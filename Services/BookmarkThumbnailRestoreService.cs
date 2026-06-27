using System.IO;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// DB にブックマークがあるがサムネイルファイルが無い場合の再作成パラメータを解決する。
    /// </summary>
    internal static class BookmarkThumbnailRestoreService
    {
        public static bool TryPrepareRestore(
            MovieRecords bookmark,
            IEnumerable<MovieRecords> library,
            out string sourceMoviePath,
            out string saveThumbPath,
            out int capturePosSeconds)
        {
            sourceMoviePath = null;
            saveThumbPath = null;
            capturePosSeconds = 0;

            if (bookmark == null)
            {
                return false;
            }

            saveThumbPath = bookmark.ThumbDetail;
            if (string.IsNullOrWhiteSpace(saveThumbPath) || File.Exists(saveThumbPath))
            {
                return false;
            }

            sourceMoviePath = BookmarkSourceResolver.ResolveSourceMoviePath(bookmark, library);
            if (string.IsNullOrWhiteSpace(sourceMoviePath) || !File.Exists(sourceMoviePath))
            {
                return false;
            }

            MovieInfo movieInfo = new(sourceMoviePath, noHash: true);
            if (movieInfo.FPS <= 0)
            {
                return false;
            }

            capturePosSeconds = (int)bookmark.Score / (int)movieInfo.FPS;
            return true;
        }
    }
}
