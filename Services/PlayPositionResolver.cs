using System.IO;
using System.Windows;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal static class PlayPositionResolver
    {
        public static int GetPlayPositionMsec(
            Point clickOnImage,
            double imageControlWidth,
            double imageControlHeight,
            int tabIndex,
            MovieRecords mv,
            ref int returnPos)
        {
            if (mv == null)
            {
                returnPos = 0;
                return 0;
            }

            string currentThumbPath = GetThumbPathForTab(mv, tabIndex);
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

        public static string GetThumbPathForTab(MovieRecords mv, int tabIndex)
        {
            if (SkinTabIndexHelper.IsWpfSkinTab(tabIndex))
            {
                return mv.ThumbPathWpfSkin;
            }

            int resolvedTab = SkinTabIndexHelper.GetThumbnailTabIndex(tabIndex);
            return resolvedTab switch
            {
                0 => mv.ThumbPathSmall,
                1 => mv.ThumbPathBig,
                2 => mv.ThumbPathGrid,
                3 => mv.ThumbPathList,
                4 => mv.ThumbPathBig10,
                SkinTabIndexHelper.WpfSkinThumbnailSlotIndex => mv.ThumbPathWpfSkin,
                _ => null,
            };
        }
    }
}
