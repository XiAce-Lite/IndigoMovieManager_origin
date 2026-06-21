using System.IO;
using System.Windows;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Services
{
    internal static class PlayPositionResolver
    {
        public static int GetPlayPositionMsec(Point clickPoint, int tabIndex, MovieRecords mv, ref int returnPos)
        {
            int msec = 0;

            string currentThumbPath = tabIndex switch
            {
                0 => mv.ThumbPathSmall,
                1 => mv.ThumbPathBig,
                2 => mv.ThumbPathGrid,
                3 => mv.ThumbPathList,
                4 => mv.ThumbPathBig10,
                _ => null,
            };

            if (currentThumbPath == null || !Path.Exists(currentThumbPath))
            {
                return 0;
            }

            ThumbInfo thumbInfo = new();
            thumbInfo.GetThumbInfo(currentThumbPath);
            if (thumbInfo.IsThumbnail != true)
            {
                return 0;
            }

            List<System.Drawing.Point> points = [];
            for (int j = 1; j < thumbInfo.ThumbRows + 1; j++)
            {
                for (int i = 1; i < thumbInfo.ThumbColumns + 1; i++)
                {
                    points.Add(new System.Drawing.Point
                    {
                        X = i * thumbInfo.ThumbWidth,
                        Y = j * thumbInfo.ThumbHeight
                    });
                }
            }

            int secPos = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                if (clickPoint.X < points[i].X && clickPoint.Y < points[i].Y)
                {
                    secPos = i;
                    break;
                }
            }

            msec = thumbInfo.ThumbSec[secPos] * 1000;
            returnPos = secPos;
            return msec;
        }
    }
}
