using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// サムネイル合成画像上のクリック位置から、パネル番号と再生位置（ms）を求める。
    /// </summary>
    internal static class ThumbPanelHitResolver
    {
        public static bool TryResolveFromImageClick(
            Point clickOnImage,
            double imageControlWidth,
            double imageControlHeight,
            string thumbPath,
            bool isZip,
            out int panelIndex,
            out int positionMsec)
        {
            panelIndex = 0;
            positionMsec = 0;

            if (string.IsNullOrWhiteSpace(thumbPath) || !File.Exists(thumbPath))
            {
                return false;
            }

            if (imageControlWidth <= 0 || imageControlHeight <= 0)
            {
                return false;
            }

            ThumbInfo thumbInfo = new();
            thumbInfo.GetThumbInfo(thumbPath);
            if (thumbInfo.IsThumbnail != true || thumbInfo.ThumbSec.Count < 1)
            {
                return false;
            }

            (int sourceWidth, int sourceHeight) = TryGetPixelSize(thumbPath);
            int columns = Math.Max(1, thumbInfo.ThumbColumns);
            int rows = Math.Max(1, thumbInfo.ThumbRows);

            if (sourceWidth <= 0)
            {
                sourceWidth = columns * Math.Max(1, thumbInfo.ThumbWidth);
            }

            if (sourceHeight <= 0)
            {
                sourceHeight = rows * Math.Max(1, thumbInfo.ThumbHeight);
            }

            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                return false;
            }

            if (!TryMapClickToCompositePixel(
                    clickOnImage,
                    imageControlWidth,
                    imageControlHeight,
                    sourceWidth,
                    sourceHeight,
                    out double pixelX,
                    out double pixelY))
            {
                return false;
            }

            int col = (int)(pixelX / (sourceWidth / (double)columns));
            int row = (int)(pixelY / (sourceHeight / (double)rows));
            col = Math.Clamp(col, 0, columns - 1);
            row = Math.Clamp(row, 0, rows - 1);
            panelIndex = col + (row * columns);

            if (panelIndex >= thumbInfo.ThumbSec.Count)
            {
                panelIndex = thumbInfo.ThumbSec.Count - 1;
            }

            positionMsec = isZip
                ? thumbInfo.ThumbSec[panelIndex]
                : thumbInfo.ThumbSec[panelIndex] * 1000;
            return true;
        }

        internal static bool TryMapClickToCompositePixel(
            Point clickOnControl,
            double controlWidth,
            double controlHeight,
            int compositePixelWidth,
            int compositePixelHeight,
            out double pixelX,
            out double pixelY)
        {
            pixelX = 0;
            pixelY = 0;

            if (controlWidth <= 0 || controlHeight <= 0 || compositePixelWidth <= 0 || compositePixelHeight <= 0)
            {
                return false;
            }

            double scale = Math.Min(
                controlWidth / compositePixelWidth,
                controlHeight / compositePixelHeight);
            if (scale <= 0)
            {
                return false;
            }

            double renderedW = compositePixelWidth * scale;
            double renderedH = compositePixelHeight * scale;
            double offsetX = (controlWidth - renderedW) / 2d;
            double offsetY = (controlHeight - renderedH) / 2d;

            pixelX = (clickOnControl.X - offsetX) / scale;
            pixelY = (clickOnControl.Y - offsetY) / scale;

            if (pixelX < 0 || pixelY < 0 || pixelX >= compositePixelWidth || pixelY >= compositePixelHeight)
            {
                return false;
            }

            return true;
        }

        private static (int Width, int Height) TryGetPixelSize(string path)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return (image.PixelWidth, image.PixelHeight);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
