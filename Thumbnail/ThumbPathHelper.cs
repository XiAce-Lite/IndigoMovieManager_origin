using System.IO;
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
            }
        }

        public static void ResolveThumbPathsForTab(
            IEnumerable<MovieRecords> records,
            ThumbnailLayoutCache cache,
            int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= cache.TabOutPaths.Length)
            {
                return;
            }

            foreach (MovieRecords item in records)
            {
                string thumbFile = ThumbnailLayoutCache.GetThumbFileName(
                    Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty),
                    item.Hash
                );
                string resolvedPath = cache.BuildThumbPath(tabIndex, thumbFile, checkExists: true);
                SetThumbPathForTab(item, tabIndex, resolvedPath);
            }
        }

        public static void ApplyThumbPaths(
            IEnumerable<MovieRecords> records,
            QueueObj queueObj,
            string saveThumbFileName)
        {
            foreach (MovieRecords item in records.Where(x => x.Movie_Id == queueObj.MovieId))
            {
                switch (queueObj.Tabindex)
                {
                    case 0: item.ThumbPathSmall = saveThumbFileName; break;
                    case 1: item.ThumbPathBig = saveThumbFileName; break;
                    case 2: item.ThumbPathGrid = saveThumbFileName; break;
                    case 3: item.ThumbPathList = saveThumbFileName; break;
                    case 4: item.ThumbPathBig10 = saveThumbFileName; break;
                    case 99: item.ThumbDetail = saveThumbFileName; break;
                }
            }
        }
    }
}
