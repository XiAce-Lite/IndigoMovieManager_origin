using System.IO;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// サムネファイル名の解決。作成側（動画パス基準）と判定側で同じ規則を使う。
    /// </summary>
    internal static class ThumbnailMovieNaming
    {
        public static string GetMovieBody(MovieRecords item)
        {
            if (item == null)
            {
                return "";
            }

            string source = !string.IsNullOrWhiteSpace(item.Movie_Path)
                ? item.Movie_Path
                : item.Movie_Name;
            return Path.GetFileNameWithoutExtension(source ?? string.Empty).ToLowerInvariant();
        }

        public static string GetThumbFileName(MovieRecords item)
        {
            string hash = item?.Hash ?? "";
            return ThumbnailLayoutCache.GetThumbFileName(GetMovieBody(item), hash);
        }

        public static string GetExpectedThumbPath(
            ThumbnailLayoutCache cache,
            MovieRecords item,
            ThumbnailLayoutSpec layout)
        {
            if (cache == null || item == null || layout == null)
            {
                return "";
            }

            return cache.GetExpectedThumbPath(layout, GetMovieBody(item), item.Hash ?? "");
        }
    }
}
