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
            Stopwatch sw = new();

            try
            {
                bool isSuccess = true;
                await Task.Run(
                    () =>
                    {
                        if (freshCapturePerPanel)
                        {
                            CaptureAllPanelsWithFreshCapture(ctx, thumbInfo, paths, ref isSuccess, sw);
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

                            CaptureAllPanels(sharedCapture, ctx, thumbInfo, paths, ref isSuccess, sw);
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
            ref bool isSuccess,
            Stopwatch sw
        )
        {
            for (int i = 0; i < thumbInfo.ThumbSec.Count; i++)
            {
                double unusedLastMsec = -1d;
                if (!CaptureSinglePanel(
                        ctx,
                        thumbInfo,
                        paths,
                        ref isSuccess,
                        sw,
                        i,
                        null,
                        useForwardCapture: false,
                        ref unusedLastMsec))
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
            ref bool isSuccess,
            Stopwatch sw
        )
        {
            bool useForwardCapture = OpenCvForwardCapturePolicy.CanUseForwardCapture(ctx, thumbInfo.ThumbSec);
            double lastCapturedMsec = -1d;

            for (int i = 0; i < thumbInfo.ThumbSec.Count; i++)
            {
                if (!CaptureSinglePanel(
                        ctx,
                        thumbInfo,
                        paths,
                        ref isSuccess,
                        sw,
                        i,
                        sharedCapture,
                        useForwardCapture,
                        ref lastCapturedMsec))
                {
                    return;
                }
            }
        }

        private static bool CaptureSinglePanel(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            List<string> paths,
            ref bool isSuccess,
            Stopwatch sw,
            int panelIndex,
            VideoCapture sharedCapture,
            bool useForwardCapture,
            ref double lastCapturedMsec)
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
                        ctx,
                        panelIndex,
                        useForwardCapture,
                        ref lastCapturedMsec,
                        out Mat dst))
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
            ThumbnailJobContext ctx,
            int panelIndex,
            bool useForwardCapture,
            ref double lastCapturedMsec,
            out Mat dst
        )
        {
            dst = null;
            double targetMsec = Math.Max(0, sec) * 1000d;

            if (!SeekToTargetMsec(capture, targetMsec, panelIndex, useForwardCapture, ref lastCapturedMsec))
            {
                return false;
            }

            using Mat img = new();
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

            int panelWidth = Math.Max(1, ctx.TabInfo.Width);
            int panelHeight = Math.Max(1, ctx.TabInfo.Height);
            dst = ThumbnailImageGeometry.FitFrameToPanel(img, panelWidth, panelHeight);
            if (dst == null || dst.Empty())
            {
                return false;
            }

            lastCapturedMsec = capture.Get(VideoCaptureProperties.PosMsec);
            if (lastCapturedMsec < 0d || double.IsNaN(lastCapturedMsec))
            {
                lastCapturedMsec = targetMsec;
            }

            return true;
        }

        private static bool SeekToTargetMsec(
            VideoCapture capture,
            double targetMsec,
            int panelIndex,
            bool useForwardCapture,
            ref double lastCapturedMsec)
        {
            if (panelIndex == 0
                || !useForwardCapture
                || lastCapturedMsec < 0d
                || targetMsec < lastCapturedMsec - 50d)
            {
                SetPosMsec(capture, targetMsec);
                return true;
            }

            double currentMsec = capture.Get(VideoCaptureProperties.PosMsec);
            if (currentMsec < 0d || double.IsNaN(currentMsec))
            {
                currentMsec = lastCapturedMsec;
            }

            double fps = capture.Get(VideoCaptureProperties.Fps);
            if (!OpenCvForwardCapturePolicy.ShouldForwardGrab(
                    currentMsec,
                    targetMsec,
                    fps,
                    OpenCvForwardCapturePolicy.DefaultMaxForwardGrabs))
            {
                SetPosMsec(capture, targetMsec);
                return true;
            }

            int grabCount = OpenCvForwardCapturePolicy.EstimateForwardGrabCount(currentMsec, targetMsec, fps);
            for (int i = 0; i < grabCount; i++)
            {
                if (capture.Get(VideoCaptureProperties.PosMsec) >= targetMsec - 50d)
                {
                    break;
                }

                if (!capture.Grab())
                {
                    SetPosMsec(capture, targetMsec);
                    return true;
                }
            }

            if (capture.Get(VideoCaptureProperties.PosMsec) < targetMsec - 50d)
            {
                SetPosMsec(capture, targetMsec);
            }

            return true;
        }

        private static void SetPosMsec(VideoCapture capture, double msec)
        {
            capture.PosMsec = (int)Math.Round(Math.Max(0d, msec));
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

            return ThumbnailCreateResult.Succeeded(paths, "OpenCV", "OpenCV");
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
