using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
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

        public static void ApplyThumbPaths(
            IEnumerable<MovieRecords> records,
            QueueObj queueObj,
            string saveThumbFileName)
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
                else if (queueObj.ThumbnailLayout.Equals(WhiteBrowserSkinSettings.GetThumbnailLayoutSpec()))
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
