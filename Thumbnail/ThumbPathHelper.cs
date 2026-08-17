using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbPathHelper
    {
        public static void ResolveThumbPathsForEngine(
            IEnumerable<MovieRecords> records,
            ThumbnailLayoutCache cache,
            SkinEngine engine)
        {
            ThumbnailLayoutResolver.ResolveThumbPathsForEngine(records, cache, engine);
        }

        /// <summary>
        /// 一覧／詳細の完了を今の画面に反映してよいか。
        /// 他スキン事前生成などはディスク作成のみ行い、ここが false なら UI パスを触らない。
        /// </summary>
        public static bool ShouldApplyToVisibleUi(QueueObj queueObj, string activeListLayoutKey)
        {
            if (queueObj?.ThumbnailLayout == null)
            {
                return false;
            }

            if (queueObj.ThumbnailLayout.Equals(ThumbnailLayoutSpec.DetailPaneLayout))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(activeListLayoutKey))
            {
                return false;
            }

            return string.Equals(
                queueObj.ThumbnailLayout.Key,
                activeListLayoutKey,
                StringComparison.OrdinalIgnoreCase);
        }

        public static void ApplyThumbPaths(
            IEnumerable<MovieRecords> records,
            QueueObj queueObj,
            string saveThumbFileName,
            SkinEngine activeEngine)
        {
            if (queueObj?.ThumbnailLayout == null)
            {
                return;
            }

            foreach (MovieRecords item in records.Where(x => x.Movie_Id == queueObj.MovieId))
            {
                if (queueObj.ThumbnailLayout.Equals(ThumbnailLayoutSpec.DetailPaneLayout))
                {
                    item.ThumbDetail = saveThumbFileName;
                }
                else if (activeEngine == SkinEngine.Wb)
                {
                    item.ThumbPathWb = saveThumbFileName;
                }
                else
                {
                    item.ThumbPathWpfSkin = saveThumbFileName;
                }
            }
        }
    }
}
