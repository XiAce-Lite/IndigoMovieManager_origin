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

            int panelCount = thumbInfo.ThumbSec.Count;
            int cols = ctx.TabInfo.Columns;
            int rows = ctx.TabInfo.Rows;
            if (panelCount < 1 || cols < 1 || rows < 1)
            {
                return ThumbnailCreateResult.Failed("invalid panel configuration");
            }

            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            double startSec = Math.Max(0, thumbInfo.ThumbSec[0]);
            double intervalSec = ResolveFrameIntervalSec(thumbInfo.ThumbSec, durationSec, panelCount);
            string vf = BuildTileFilter(
                intervalSec,
                targetWidth,
                targetHeight,
                cols,
                rows,
                durationSec,
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
                $"if(lte(abs(dar-{targetAspectText}),0.01),{width},if(gte(dar,{targetAspectText}),{width},-2))";
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
