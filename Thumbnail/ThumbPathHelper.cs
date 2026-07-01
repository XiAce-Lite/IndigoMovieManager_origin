using System.IO;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbPathHelper
    {
        public static void SetThumbPathForTab(MovieRecords item, int tabIndex, string path)
        {
            switch (tabIndex)
            {
                case 0: item.ThumbPathSmall = path; break;
                case 1: item.ThumbPathBig = path; break;
                case 2: item.ThumbPathGrid = path; break;
                case 3: item.ThumbPathList = path; break;
                case 4: item.ThumbPathBig10 = path; break;
                case SkinTabIndexHelper.WpfSkinThumbnailSlotIndex:
                    item.ThumbPathWpfSkin = path;
                    break;
            }
        }

        public static void ResolveThumbPathsForEngine(
            IEnumerable<MovieRecords> records,
            ThumbnailLayoutCache cache,
            SkinEngine engine)
        {
            ThumbnailLayoutResolver.ResolveThumbPathsForEngine(records, cache, engine);
        }

        public static void ResolveThumbPathsForTab(
            IEnumerable<MovieRecords> records,
            ThumbnailLayoutCache cache,
            int tabIndex)
        {
            ResolveThumbPathsForEngine(
                records,
                cache,
                SkinEngineHelper.FromLegacyThumbTabIndex(tabIndex));
        }

        public static void ApplyThumbPaths(
            IEnumerable<MovieRecords> records,
            QueueObj queueObj,
            string saveThumbFileName)
        {
            foreach (MovieRecords item in records.Where(x => x.Movie_Id == queueObj.MovieId))
            {
                if (queueObj.ThumbnailLayout != null)
                {
                    if (queueObj.ThumbnailLayout.Equals(WhiteBrowserSkinSettings.GetThumbnailLayoutSpec()))
                    {
                        item.ThumbPathWb = saveThumbFileName;
                    }
                    else
                    {
                        item.ThumbPathWpfSkin = saveThumbFileName;
                    }

                    continue;
                }

                switch (queueObj.Tabindex)
                {
                    case 0: item.ThumbPathSmall = saveThumbFileName; break;
                    case 1: item.ThumbPathBig = saveThumbFileName; break;
                    case 2: item.ThumbPathGrid = saveThumbFileName; break;
                    case 3: item.ThumbPathList = saveThumbFileName; break;
                    case 4: item.ThumbPathBig10 = saveThumbFileName; break;
                    case SkinTabIndexHelper.WpfSkinThumbnailSlotIndex:
                        item.ThumbPathWpfSkin = saveThumbFileName;
                        break;
                    case 99: item.ThumbDetail = saveThumbFileName; break;
                }
            }
        }
    }
}
