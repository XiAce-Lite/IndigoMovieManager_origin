using System.IO;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// アクティブスキンエンジンから一覧サムネの <see cref="ThumbnailLayoutSpec"/> を解決する。
    /// </summary>
    internal static class ThumbnailLayoutResolver
    {
        public static ThumbnailLayoutSpec GetActiveListLayout(SkinEngine engine) =>
            engine == SkinEngine.Wb
                ? WhiteBrowserSkinSettings.GetThumbnailLayoutSpec()
                : WpfSkinSettings.CurrentThumbnailLayout
                    ?? new ThumbnailLayoutSpec(400, 225, 1, 1);

        public static void ResolveThumbPathsForEngine(
            IEnumerable<MovieRecords> records,
            ThumbnailLayoutCache cache,
            SkinEngine engine)
        {
            if (records == null || cache == null)
            {
                return;
            }

            ThumbnailLayoutSpec spec = GetActiveListLayout(engine);
            foreach (MovieRecords item in records)
            {
                string thumbFile = ThumbnailLayoutCache.GetThumbFileName(
                    Path.GetFileNameWithoutExtension(item.Movie_Name ?? item.Movie_Path ?? string.Empty),
                    item.Hash);
                string path = cache.BuildThumbPath(spec, thumbFile, checkExists: true);
                if (engine == SkinEngine.Wb)
                {
                    item.ThumbPathWb = path;
                }
                else
                {
                    item.ThumbPathWpfSkin = path;
                }
            }
        }

        public static string GetTrackLayoutKey(QueueObj queueObj) =>
            queueObj?.ThumbnailLayout?.Key ?? "";
    }
}
