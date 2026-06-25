using System.IO;
using System.Windows;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Services
{
    internal static class PlayPositionResolver
    {
        public static int GetPlayPositionMsec(Point clickPoint, int tabIndex, MovieRecords mv, ref int returnPos)
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

            ThumbInfo thumbInfo = new();
            thumbInfo.GetThumbInfo(currentThumbPath);
            if (thumbInfo.IsThumbnail != true || thumbInfo.ThumbSec.Count < 1)
            {
                returnPos = 0;
                return 0;
            }

            int columns = Math.Max(1, thumbInfo.ThumbColumns);
            int rows = Math.Max(1, thumbInfo.ThumbRows);
            int panelCount = columns * rows;
            if (panelCount <= 0)
            {
                returnPos = 0;
                return 0;
            }

            int sourceWidth = columns * Math.Max(1, thumbInfo.ThumbWidth);
            int sourceHeight = rows * Math.Max(1, thumbInfo.ThumbHeight);
            (int fileWidth, int fileHeight) = TryGetFilePixelSize(currentThumbPath);
            if (fileWidth > 0)
            {
                sourceWidth = fileWidth;
            }

            if (fileHeight > 0)
            {
                sourceHeight = fileHeight;
            }

            double cellWidth = sourceWidth / (double)columns;
            double cellHeight = sourceHeight / (double)rows;
            int col = (int)(clickPoint.X / cellWidth);
            int row = (int)(clickPoint.Y / cellHeight);
            col = Math.Clamp(col, 0, columns - 1);
            row = Math.Clamp(row, 0, rows - 1);
            int secPos = col + (row * columns);

            if (secPos >= thumbInfo.ThumbSec.Count)
            {
                secPos = thumbInfo.ThumbSec.Count - 1;
            }

            returnPos = secPos;
            return ZipMediaKind.IsZipRecord(mv)
                ? thumbInfo.ThumbSec[secPos]
                : thumbInfo.ThumbSec[secPos] * 1000;
        }

        public static string GetThumbPathForTab(MovieRecords mv, int tabIndex) =>
            tabIndex switch
            {
                0 => mv.ThumbPathSmall,
                1 => mv.ThumbPathBig,
                2 => mv.ThumbPathGrid,
                3 => mv.ThumbPathList,
                4 => mv.ThumbPathBig10,
                _ => null,
            };

        private static (int Width, int Height) TryGetFilePixelSize(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var image = System.Drawing.Image.FromStream(stream, false, false);
                return (image.Width, image.Height);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
