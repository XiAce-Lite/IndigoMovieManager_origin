using System.IO;
using System.Windows;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal static class PlayPositionResolver
    {
        public static int GetPlayPositionMsec(
            Point clickOnImage,
            double imageControlWidth,
            double imageControlHeight,
            SkinEngine engine,
            MovieRecords mv,
            ref int returnPos)
        {
            if (mv == null)
            {
                returnPos = 0;
                return 0;
            }

            string currentThumbPath = GetThumbPathForEngine(mv, engine);
            if (string.IsNullOrWhiteSpace(currentThumbPath) || !File.Exists(currentThumbPath))
            {
                returnPos = 0;
                return 0;
            }

            if (ThumbPanelHitResolver.TryResolveFromImageClick(
                    clickOnImage,
                    imageControlWidth,
                    imageControlHeight,
                    currentThumbPath,
                    ZipMediaKind.IsZipRecord(mv),
                    out int panelIndex,
                    out int positionMsec))
            {
                returnPos = panelIndex;
                return positionMsec;
            }

            returnPos = 0;
            return 0;
        }

        public static string GetThumbPathForEngine(MovieRecords mv, SkinEngine engine) =>
            engine == SkinEngine.Wb
                ? mv.ThumbPathWb
                : mv.ThumbPathWpfSkin;

        public static string GetThumbPathForTab(MovieRecords mv, int tabIndex) =>
            GetThumbPathForEngine(mv, SkinEngineHelper.FromLegacyThumbTabIndex(tabIndex));
    }
}
