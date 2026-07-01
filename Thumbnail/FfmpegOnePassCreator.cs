using System.Globalization;
using System.IO;
using System.Text;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 派生 private engine の ffmpeg1pass を参考に、1 プロセスで tile サムネを生成する。
    /// -ss ThumbSec[0] から fps=1/interval で等間隔フレームを取り tile 合成（0 秒起点ではない）。
    /// </summary>
    internal static class FfmpegOnePassCreator
    {
        private const string JpegQualityEnvName = "IMM_THUMB_JPEG_Q";
        private const string ScaleFlagsEnvName = "IMM_THUMB_SCALE_FLAGS";
        private const int DefaultJpegQuality = 5;
        private static readonly TimeSpan OnePassTimeout = TimeSpan.FromMinutes(6);

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
                return ThumbnailCreateResult.Failed("manual mode is not supported in ffmpeg one-pass");
            }

            if (!FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec))
            {
                return ThumbnailCreateResult.Failed("thumb sec layout is not eligible for ffmpeg one-pass");
            }

            int panelCount = thumbInfo.ThumbSec.Count;
            int cols = ctx.TabInfo.Columns;
            int rows = ctx.TabInfo.Rows;
            if (panelCount < 1 || cols < 1 || rows < 1 || cols * rows != panelCount)
            {
                return ThumbnailCreateResult.Failed("invalid panel configuration for ffmpeg one-pass");
            }

            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            double startSec = FfmpegOnePassPolicy.ResolveStartSec(thumbInfo);
            double intervalSec = FfmpegOnePassPolicy.ResolveIntervalSec(thumbInfo, durationSec);
            int jpegQuality = ResolveJpegQuality();
            string scaleFlags = ResolveScaleFlags();
            string startText = startSec.ToString("0.###", CultureInfo.InvariantCulture);
            string vf = BuildTileFilter(
                intervalSec,
                targetWidth,
                targetHeight,
                cols,
                rows,
                durationSec,
                panelCount,
                scaleFlags);

            string saveDir = Path.GetDirectoryName(ctx.SaveThumbFileName) ?? "";
            if (!string.IsNullOrWhiteSpace(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            if (File.Exists(ctx.SaveThumbFileName))
            {
                File.Delete(ctx.SaveThumbFileName);
            }

            string lastError = "ffmpeg one-pass failed";

            (bool ok, string stderr, string decoder) = await RunFfmpegWithHardwareFallbackAsync(
                    ffmpegExePath,
                    hwMode => BuildOnePassArgs(
                        hwMode,
                        startText,
                        ctx.MovieFullPath,
                        jpegQuality,
                        vf,
                        ctx.SaveThumbFileName),
                    OnePassTimeout,
                    cts)
                .ConfigureAwait(false);

            if (!ok || !File.Exists(ctx.SaveThumbFileName))
            {
                lastError = string.IsNullOrWhiteSpace(stderr) ? lastError : stderr;
                return ThumbnailCreateResult.Failed(lastError, "FFmpeg1Pass", decoder);
            }

            ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
            return ThumbnailCreateResult.Succeeded(
                [ctx.SaveThumbFileName],
                "FFmpeg1Pass",
                decoder);
        }

        internal static string BuildTileFilter(
            double intervalSec,
            int width,
            int height,
            int cols,
            int rows,
            double durationSec,
            int panelCount,
            string scaleFlags
        )
        {
            double safeInterval = intervalSec > 0 ? intervalSec : 1d;
            string intervalText = safeInterval.ToString("0.###", CultureInfo.InvariantCulture);
            StringBuilder vf = new();

            if (durationSec > 0
                && panelCount > 0
                && durationSec < safeInterval * panelCount)
            {
                double padSec = (safeInterval * panelCount) - durationSec + 0.05;
                string padText = padSec.ToString("0.###", CultureInfo.InvariantCulture);
                vf.Append($"tpad=stop_mode=clone:stop_duration={padText},");
            }

            vf.Append($"fps=1/{intervalText},");
            vf.Append(BuildAspectFillCropFilter(width, height, scaleFlags));
            vf.Append(',');
            vf.Append($"tile={cols}x{rows}");
            return vf.ToString();
        }

        private static List<string> BuildOnePassArgs(
            FfmpegHardwareDecodeMode? hwMode,
            string startText,
            string inputPath,
            int jpegQuality,
            string vf,
            string outputPath
        )
        {
            List<string> args =
            [
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-an",
                "-sn",
                "-dn",
            ];

            if (hwMode is FfmpegHardwareDecodeMode mode && mode != FfmpegHardwareDecodeMode.Off)
            {
                string hwaccel = FfmpegHardwareDecodePolicy.GetHwaccelName(mode);
                if (!string.IsNullOrWhiteSpace(hwaccel))
                {
                    args.Add("-hwaccel");
                    args.Add(hwaccel);
                }
            }

            args.Add("-ss");
            args.Add(startText);
            args.Add("-i");
            args.Add(inputPath);
            args.Add("-frames:v");
            args.Add("1");
            args.Add("-strict");
            args.Add("unofficial");
            args.Add("-pix_fmt");
            args.Add("yuv420p");
            args.Add("-q:v");
            args.Add(jpegQuality.ToString(CultureInfo.InvariantCulture));
            args.Add("-vf");
            args.Add(vf);
            args.Add(outputPath);
            return args;
        }

        private static string BuildAspectFillCropFilter(int width, int height, string scaleFlags)
        {
            return
                $"scale={width}:{height}:force_original_aspect_ratio=increase:flags={scaleFlags},"
                + $"crop={width}:{height},setsar=1";
        }

        private static (int width, int height) ResolveTargetSize(ThumbnailJobContext ctx)
        {
            int w = ctx.TabInfo?.Width ?? 0;
            int h = ctx.TabInfo?.Height ?? 0;
            if (w > 0 && h > 0)
            {
                return (w, h);
            }

            return (160, 120);
        }

        private static async Task<(bool ok, string stderr, string decoderLabel)> RunFfmpegWithHardwareFallbackAsync(
            string ffmpegExePath,
            Func<FfmpegHardwareDecodeMode?, List<string>> buildArgs,
            TimeSpan timeout,
            CancellationToken cts
        )
        {
            IReadOnlyList<FfmpegHardwareDecodeMode> modes =
                FfmpegHardwareDecodePolicy.GetModesToAttempt(ffmpegExePath);

            foreach (FfmpegHardwareDecodeMode mode in modes)
            {
                (bool ok, string stderr) = await FfmpegProcessRunner
                    .RunAsync(ffmpegExePath, buildArgs(mode), timeout, cts)
                    .ConfigureAwait(false);

                if (ok)
                {
                    return (true, stderr, FfmpegHardwareDecodePolicy.GetHwaccelName(mode));
                }

                FfmpegHardwareDecodePolicy.MarkModeFailed(mode);
            }

            (bool softwareOk, string softwareStderr) = await FfmpegProcessRunner
                .RunAsync(ffmpegExePath, buildArgs(null), timeout, cts)
                .ConfigureAwait(false);

            return (softwareOk, softwareStderr, "software");
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

            return DefaultJpegQuality;
        }

        private static string ResolveScaleFlags()
        {
            string raw = Environment.GetEnvironmentVariable(ScaleFlagsEnvName)?.Trim() ?? "";
            return raw.ToLowerInvariant() switch
            {
                "nearest" => "nearest",
                "bilinear" => "bilinear",
                "bicubic" => "bicubic",
                "lanczos" => "lanczos",
                _ => "bilinear",
            };
        }
    }
}
