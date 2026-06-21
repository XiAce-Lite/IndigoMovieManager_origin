using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegFallbackCreator
    {
        private const string JpegQualityEnvName = "IMM_THUMB_JPEG_Q";
        private const int DefaultJpegQuality = 5;

        public static async Task<ThumbnailCreateResult> TryCreateAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            double durationSec,
            string ffmpegExePath,
            CancellationToken cts
        )
        {
            if (ctx == null || thumbInfo == null)
            {
                return ThumbnailCreateResult.Failed("context or thumbInfo is null");
            }

            if (ctx.IsManual)
            {
                return ThumbnailCreateResult.Failed("manual mode is not supported in ffmpeg fallback (phase 1)");
            }

            if (thumbInfo.ThumbSec == null || thumbInfo.ThumbSec.Count < 1)
            {
                return ThumbnailCreateResult.Failed("thumb sec list is empty");
            }

            ThumbnailCreateResult tiledResult = await TryCreateTiledAsync(
                ctx,
                thumbInfo,
                durationSec,
                ffmpegExePath,
                cts).ConfigureAwait(false);

            if (tiledResult.Success)
            {
                return tiledResult;
            }

            ThumbnailCreateResult singleFrameResult = await TryCreateSingleFrameFallbackAsync(
                ctx,
                thumbInfo,
                durationSec,
                ffmpegExePath,
                cts).ConfigureAwait(false);

            if (singleFrameResult.Success)
            {
                return singleFrameResult;
            }

            string reason = tiledResult.FailureReason ?? "ffmpeg failed";
            if (!string.IsNullOrWhiteSpace(singleFrameResult.FailureReason))
            {
                reason = $"{reason}; single-frame: {singleFrameResult.FailureReason}";
            }

            return ThumbnailCreateResult.Failed(reason);
        }

        private static async Task<ThumbnailCreateResult> TryCreateTiledAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            double durationSec,
            string ffmpegExePath,
            CancellationToken cts
        )
        {
            int panelCount = thumbInfo.ThumbSec.Count;
            int cols = ctx.TabInfo.Columns;
            int rows = ctx.TabInfo.Rows;
            if (panelCount < 1 || cols < 1 || rows < 1)
            {
                return ThumbnailCreateResult.Failed("invalid panel configuration");
            }

            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            double startSec = Math.Max(0, thumbInfo.ThumbSec[0]);
            double samplingDuration = ThumbnailSamplingPolicy.GetEffectiveSamplingDuration(
                durationSec,
                ctx.IsManual);
            double intervalSec = ResolveFrameIntervalSec(thumbInfo.ThumbSec, samplingDuration, panelCount);
            string vf = BuildTileFilter(
                intervalSec,
                targetWidth,
                targetHeight,
                cols,
                rows,
                samplingDuration,
                panelCount
            );

            string startText = startSec.ToString("0.###", CultureInfo.InvariantCulture);
            int jpegQuality = ResolveJpegQuality();

            List<string> args =
            [
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-an",
                "-sn",
                "-dn",
                "-ss",
                startText,
                "-i",
                ctx.MovieFullPath,
                "-frames:v",
                "1",
                "-strict",
                "unofficial",
                "-pix_fmt",
                "yuv420p",
                "-q:v",
                jpegQuality.ToString(CultureInfo.InvariantCulture),
                "-vf",
                vf,
                ctx.SaveThumbFileName,
            ];

            TimeSpan timeout = TimeSpan.FromSeconds(Math.Min(600, Math.Max(60, panelCount * 15)));
            (bool ok, string stderr) = await FfmpegProcessRunner
                .RunAsync(ffmpegExePath, args, timeout, cts)
                .ConfigureAwait(false);

            if (!ok || !File.Exists(ctx.SaveThumbFileName))
            {
                return ThumbnailCreateResult.Failed(
                    string.IsNullOrWhiteSpace(stderr) ? "ffmpeg one-pass failed" : stderr
                );
            }

            ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
            return ThumbnailCreateResult.Succeeded([ctx.SaveThumbFileName]);
        }

        /// <summary>
        /// タイル合成に失敗したファイル向け。1 フレームだけ抽出し、同一画像を並べてサムネを構成する。
        /// </summary>
        private static async Task<ThumbnailCreateResult> TryCreateSingleFrameFallbackAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            double durationSec,
            string ffmpegExePath,
            CancellationToken cts
        )
        {
            int panelCount = thumbInfo.ThumbSec.Count;
            int cols = ctx.TabInfo.Columns;
            int rows = ctx.TabInfo.Rows;
            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            int jpegQuality = ResolveJpegQuality();
            string tempFile = Path.Combine(ctx.TempPath, $"tn_{ctx.TempFileBody}_ffsingle.jpg");
            string lastError = "single-frame extract failed";

            foreach (double seekSec in BuildSingleFrameSeekPoints(durationSec, thumbInfo))
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                string seekText = seekSec.ToString("0.###", CultureInfo.InvariantCulture);
                List<string> args =
                [
                    "-y",
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-an",
                    "-sn",
                    "-dn",
                    "-ss",
                    seekText,
                    "-i",
                    ctx.MovieFullPath,
                    "-frames:v",
                    "1",
                    "-pix_fmt",
                    "yuv420p",
                    "-q:v",
                    jpegQuality.ToString(CultureInfo.InvariantCulture),
                    "-vf",
                    BuildAspectFitScaleAndPadFilter(targetWidth, targetHeight),
                    tempFile,
                ];

                (bool ok, string stderr) = await FfmpegProcessRunner
                    .RunAsync(ffmpegExePath, args, TimeSpan.FromSeconds(90), cts)
                    .ConfigureAwait(false);

                if (!ok || !File.Exists(tempFile))
                {
                    lastError = string.IsNullOrWhiteSpace(stderr) ? lastError : stderr;
                    continue;
                }

                try
                {
                    List<string> panelPaths = [];
                    for (int i = 0; i < panelCount; i++)
                    {
                        panelPaths.Add(tempFile);
                    }

                    using Bitmap bmp = ConcatImages(panelPaths, cols, rows);
                    if (bmp == null)
                    {
                        lastError = "single-frame concat failed";
                        continue;
                    }

                    if (File.Exists(ctx.SaveThumbFileName))
                    {
                        File.Delete(ctx.SaveThumbFileName);
                    }

                    bmp.Save(ctx.SaveThumbFileName, ImageFormat.Jpeg);
                    ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
                    return ThumbnailCreateResult.Succeeded([ctx.SaveThumbFileName]);
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }

            return ThumbnailCreateResult.Failed(lastError);
        }

        private static IEnumerable<double> BuildSingleFrameSeekPoints(double durationSec, ThumbInfo thumbInfo)
        {
            LinkedList<double> points = [];
            double maxSeekSec = ThumbnailSamplingPolicy.GetEffectiveSamplingDuration(durationSec, isManual: false);
            if (maxSeekSec <= 0d)
            {
                maxSeekSec = ThumbnailSamplingPolicy.VirtualDurationWindowSec;
            }

            void Add(double sec)
            {
                if (sec < 0)
                {
                    return;
                }

                if (sec > maxSeekSec)
                {
                    return;
                }

                if (!points.Any(existing => Math.Abs(existing - sec) < 0.001d))
                {
                    points.AddLast(sec);
                }
            }

            if (thumbInfo.ThumbSec.Count > 0)
            {
                Add(thumbInfo.ThumbSec[0]);
            }

            Add(1d);
            Add(0d);

            if (durationSec > 2 && maxSeekSec >= durationSec * 0.5d)
            {
                Add(durationSec * 0.5d);
            }

            return points;
        }

        private static (int width, int height) ResolveTargetSize(ThumbnailJobContext ctx)
        {
            if (ctx.IsResizeThumb && ctx.TabInfo.Width > 0 && ctx.TabInfo.Height > 0)
            {
                return (ctx.TabInfo.Width, ctx.TabInfo.Height);
            }

            return (320, 240);
        }

        private static double ResolveFrameIntervalSec(
            List<int> secList,
            double durationSec,
            int panelCount
        )
        {
            if (secList.Count >= 2)
            {
                int interval = secList[1] - secList[0];
                if (interval > 0)
                {
                    return interval;
                }
            }

            if (durationSec > 0 && panelCount > 0)
            {
                double divide = durationSec / (panelCount + 1);
                if (divide > 0.1d)
                {
                    return divide;
                }
            }

            return 1d;
        }

        private static string BuildTileFilter(
            double intervalSec,
            int width,
            int height,
            int cols,
            int rows,
            double durationSec,
            int panelCount
        )
        {
            double safeInterval = intervalSec > 0 ? intervalSec : 1d;
            string intervalText = safeInterval.ToString("0.###", CultureInfo.InvariantCulture);
            StringBuilder vf = new();

            if (durationSec > 0 && panelCount > 0 && durationSec < safeInterval * panelCount)
            {
                double padSec = (safeInterval * panelCount) - durationSec + 0.05d;
                string padText = padSec.ToString("0.###", CultureInfo.InvariantCulture);
                vf.Append($"tpad=stop_mode=clone:stop_duration={padText},");
            }

            vf.Append($"fps=1/{intervalText},");
            vf.Append(BuildAspectFitScaleAndPadFilter(width, height));
            vf.Append(',');
            vf.Append($"tile={cols}x{rows}");
            return vf.ToString();
        }

        private static string BuildAspectFitScaleAndPadFilter(int width, int height)
        {
            string targetAspectText = ((double)width / height).ToString(
                "0.############",
                CultureInfo.InvariantCulture
            );

            string scaleWidthExpr =
                $"if(lte(abs(dar-{targetAspectText}),0.01),{width},if(gte(dar-{targetAspectText}),{width},-2))";
            string scaleHeightExpr =
                $"if(lte(abs(dar-{targetAspectText}),0.01),{height},if(gte(dar,{targetAspectText}),-2,{height}))";

            return
                $"scale='{scaleWidthExpr}':'{scaleHeightExpr}':flags=lanczos,setsar=1,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black";
        }

        private static int ResolveJpegQuality()
        {
            string raw = Environment.GetEnvironmentVariable(JpegQualityEnvName)?.Trim() ?? "";
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed >= 2
                && parsed <= 31)
            {
                return parsed;
            }

            int configured = Properties.Settings.Default.FfmpegJpegQuality;
            if (configured >= 2 && configured <= 31)
            {
                return configured;
            }

            return DefaultJpegQuality;
        }
    }
}
