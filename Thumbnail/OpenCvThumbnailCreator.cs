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
        public static Task<ThumbnailCreateResult> TryCreateAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            CancellationToken cts
        ) => TryCreateInternalAsync(ctx, thumbInfo, freshCapturePerPanel: false, cts);

        public static Task<ThumbnailCreateResult> TryCreatePerPanelAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            CancellationToken cts
        ) => TryCreateInternalAsync(ctx, thumbInfo, freshCapturePerPanel: true, cts);

        private static async Task<ThumbnailCreateResult> TryCreateInternalAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            bool freshCapturePerPanel,
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
                bool isSuccess = true;
                await Task.Run(
                    () =>
                    {
                        if (freshCapturePerPanel)
                        {
                            CaptureAllPanelsWithFreshCapture(ctx, thumbInfo, paths, ref sz, ref isSuccess, sw);
                        }
                        else
                        {
                            using VideoCapture sharedCapture = OpenVideoCapture(ctx.MovieFullPath);
                            sharedCapture.Grab();
                            if (!sharedCapture.IsOpened())
                            {
                                isSuccess = false;
                                return;
                            }

                            CaptureAllPanels(sharedCapture, ctx, thumbInfo, paths, ref sz, ref isSuccess, sw);
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

                return FinalizeThumbnail(ctx, thumbInfo, paths);
            }
            catch (Exception ex)
            {
                return ThumbnailCreateResult.Failed(ex.Message);
            }
        }

        private static void CaptureAllPanelsWithFreshCapture(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            List<string> paths,
            ref OpenCvSharp.Size sz,
            ref bool isSuccess,
            Stopwatch sw
        )
        {
            for (int i = 0; i < thumbInfo.ThumbSec.Count; i++)
            {
                if (!CaptureSinglePanel(ctx, thumbInfo, paths, ref sz, ref isSuccess, sw, i, null))
                {
                    return;
                }
            }
        }

        private static void CaptureAllPanels(
            VideoCapture sharedCapture,
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            List<string> paths,
            ref OpenCvSharp.Size sz,
            ref bool isSuccess,
            Stopwatch sw
        )
        {
            for (int i = 0; i < thumbInfo.ThumbSec.Count; i++)
            {
                if (!CaptureSinglePanel(ctx, thumbInfo, paths, ref sz, ref isSuccess, sw, i, sharedCapture))
                {
                    return;
                }
            }
        }

        private static bool CaptureSinglePanel(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            List<string> paths,
            ref OpenCvSharp.Size sz,
            ref bool isSuccess,
            Stopwatch sw,
            int panelIndex,
            VideoCapture sharedCapture
        )
        {
            sw.Restart();

            VideoCapture ownedCapture = null;
            VideoCapture capture = sharedCapture;
            if (sharedCapture == null)
            {
                ownedCapture = OpenVideoCapture(ctx.MovieFullPath);
                capture = ownedCapture;
                capture.Grab();
                if (!capture.IsOpened())
                {
                    ownedCapture.Dispose();
                    isSuccess = false;
                    return false;
                }
            }

            try
            {
                if (!TryCapturePanelAtSec(
                        capture,
                        thumbInfo.ThumbSec[panelIndex],
                        out Mat dst,
                        ref sz,
                        ctx))
                {
                    dst?.Dispose();
                    isSuccess = false;
                    return false;
                }

                sw.Stop();
                if (sw.Elapsed.TotalSeconds > 60)
                {
                    dst.Dispose();
                    isSuccess = false;
                    return false;
                }

                string saveFile = Path.Combine(ctx.TempPath, $"tn_{ctx.TempFileBody}{panelIndex:D2}.jpg");
                BitmapConverter.ToBitmap(dst).Save(saveFile, ImageFormat.Jpeg);
                dst.Dispose();
                paths.Add(saveFile);
                return true;
            }
            finally
            {
                ownedCapture?.Dispose();
            }
        }

        private static bool TryCapturePanelAtSec(
            VideoCapture capture,
            int sec,
            out Mat dst,
            ref OpenCvSharp.Size sz,
            ThumbnailJobContext ctx
        )
        {
            dst = null;
            using Mat img = new();
            capture.PosMsec = sec * 1000;

            int msecCounter = 0;
            while (!capture.Read(img))
            {
                capture.PosMsec += 100;
                if (msecCounter > 100)
                {
                    return false;
                }

                msecCounter++;
            }

            if (img.Empty() || img.Width == 0 || img.Height == 0)
            {
                return false;
            }

            using Mat temp = new(img, ThumbnailImageGeometry.GetAspect(img.Width, img.Height));

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

            dst = new Mat();
            Cv2.Resize(temp, dst, sz);
            return true;
        }

        private static ThumbnailCreateResult FinalizeThumbnail(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            List<string> paths
        )
        {
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

            return ThumbnailCreateResult.Succeeded(paths);
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

        internal static void CleanupTempPanels(ThumbnailJobContext ctx)
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
