using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class OpenCvThumbnailCreator
    {
        public static async Task<ThumbnailCreateResult> TryCreateAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            CancellationToken cts
        )
        {
            if (ctx == null || thumbInfo == null)
            {
                return ThumbnailCreateResult.Failed("context or thumbInfo is null");
            }

            DeleteOldTempFiles(ctx);

            List<string> paths = [];
            OpenCvSharp.Size sz = new(0, 0);
            Stopwatch sw = new();

            try
            {
                using VideoCapture capture = OpenVideoCapture(ctx.MovieFullPath);
                capture.Grab();

                if (!capture.IsOpened())
                {
                    return ThumbnailCreateResult.Failed("VideoCapture open failed");
                }

                bool isSuccess = true;
                await Task.Run(
                    () =>
                    {
                        for (int i = 0; i < thumbInfo.ThumbSec.Count; i++)
                        {
                            sw.Restart();

                            using Mat img = new();
                            capture.PosMsec = thumbInfo.ThumbSec[i] * 1000;

                            int msecCounter = 0;
                            while (capture.Read(img) == false)
                            {
                                capture.PosMsec += 100;
                                if (msecCounter > 100)
                                {
                                    break;
                                }

                                msecCounter++;
                            }

                            sw.Stop();
                            if (sw.Elapsed.TotalSeconds > 60)
                            {
                                isSuccess = false;
                                return;
                            }

                            if (img.Empty() || img.Width == 0 || img.Height == 0)
                            {
                                isSuccess = false;
                                return;
                            }

                            using Mat temp = new(img, ThumbnailImageGeometry.GetAspect(img.Width, img.Height));
                            string saveFile = Path.Combine(ctx.TempPath, $"tn_{ctx.TempFileBody}{i:D2}.jpg");

                            if (ctx.IsResizeThumb)
                            {
                                sz = new OpenCvSharp.Size
                                {
                                    Width = ctx.TabInfo.Width,
                                    Height = ctx.TabInfo.Height,
                                };
                            }
                            else if (sz.Width == 0)
                            {
                                sz = new OpenCvSharp.Size
                                {
                                    Width = temp.Width < 320 ? temp.Width : 320,
                                    Height = temp.Height < 240 ? temp.Height : 240,
                                };
                            }

                            using Mat dst = new();
                            Cv2.Resize(temp, dst, sz);
                            BitmapConverter.ToBitmap(dst).Save(saveFile, ImageFormat.Jpeg);
                            paths.Add(saveFile);
                        }
                    },
                    cts
                ).ConfigureAwait(false);

                if (!isSuccess)
                {
                    return ThumbnailCreateResult.Failed("opencv frame capture failed or timed out");
                }

                if (paths.Count != thumbInfo.ThumbSec.Count)
                {
                    return ThumbnailCreateResult.Failed("opencv produced incomplete panel set");
                }

                Bitmap bmp = ConcatImages(paths, ctx.TabInfo.Columns, ctx.TabInfo.Rows);
                if (bmp == null)
                {
                    return ThumbnailCreateResult.Failed("opencv concat failed");
                }

                if (File.Exists(ctx.SaveThumbFileName))
                {
                    File.Delete(ctx.SaveThumbFileName);
                }

                bmp.Save(ctx.SaveThumbFileName, ImageFormat.Jpeg);
                bmp.Dispose();
                ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);

#if DEBUG == false
                CleanupTempPanels(ctx);
#endif

                return ThumbnailCreateResult.Succeeded(paths);
            }
            catch (Exception ex)
            {
                return ThumbnailCreateResult.Failed(ex.Message);
            }
        }

        private static VideoCapture OpenVideoCapture(string movieFullPath)
        {
            VideoCapture ffmpegCapture = new(movieFullPath, VideoCaptureAPIs.FFMPEG);
            if (ffmpegCapture.IsOpened())
            {
                return ffmpegCapture;
            }

            ffmpegCapture.Dispose();
            return new VideoCapture(movieFullPath);
        }

        private static void DeleteOldTempFiles(ThumbnailJobContext ctx)
        {
            string[] oldTempFiles = Directory.GetFiles(
                ctx.TempPath,
                $"*{ctx.TempFileBody}*.jpg",
                SearchOption.TopDirectoryOnly
            );
            foreach (string oldFile in oldTempFiles)
            {
                if (File.Exists(oldFile))
                {
                    File.Delete(oldFile);
                }
            }
        }

        private static void CleanupTempPanels(ThumbnailJobContext ctx)
        {
            string[] oldTempFiles = Directory.GetFiles(
                ctx.TempPath,
                $"*{ctx.TempFileBody}*.jpg",
                SearchOption.TopDirectoryOnly
            );
            Parallel.ForEach(
                oldTempFiles,
                oldFile =>
                {
                    if (File.Exists(oldFile))
                    {
                        File.Delete(oldFile);
                    }
                }
            );
        }
    }
}
