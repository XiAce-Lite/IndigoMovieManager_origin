using System.Drawing.Imaging;
using System.IO;
using IndigoMovieManager.Thumbnail;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace IndigoMovieManager.Thumbnail
{
    internal static class BookmarkThumbnailCreator
    {
        public static async Task CreateAsync(string movieFullPath, string saveThumbPath, int capturePos)
        {
            if (!Path.Exists(movieFullPath))
            {
                return;
            }

            await Task.Run(() =>
            {
                using VideoCapture capture = new(movieFullPath);
                capture.Grab();

                using Mat img = new();
                capture.PosMsec = capturePos * 1000;
                int msecCounter = 0;
                while (capture.Read(img) == false)
                {
                    capture.PosMsec += 100;
                    if (msecCounter > 100) { break; }
                    msecCounter++;
                }

                if (img == null || img.Width == 0 || img.Height == 0)
                {
                    return;
                }

                using Mat temp = new(img, ThumbnailImageGeometry.GetAspect(img.Width, img.Height));
                using Mat dst = new();
                OpenCvSharp.Size sz = new(640, 480);
                Cv2.Resize(temp, dst, sz);
                BitmapConverter.ToBitmap(dst).Save(saveThumbPath, ImageFormat.Jpeg);
            }).ConfigureAwait(false);

            await Task.Delay(1000).ConfigureAwait(false);
        }
    }
}
