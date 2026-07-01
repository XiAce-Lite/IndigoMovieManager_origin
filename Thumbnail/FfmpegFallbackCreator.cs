using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class FfmpegFallbackCreator
    {
        private const string JpegQualityEnvName = "IMM_THUMB_JPEG_Q";
        private const string BenchPerPanelOnlyEnvName = "IMM_THUMB_BENCH_PERPANEL_ONLY";
        private const int DefaultJpegQuality = 5;
        private static readonly TimeSpan PerPanelTimeout = TimeSpan.FromSeconds(90);

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

            if (!IsBenchPerPanelOnly() && FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec))
            {
                ThumbnailCreateResult onePassResult = await FfmpegOnePassCreator
                    .TryCreateAsync(ctx, thumbInfo, durationSec, ffmpegExePath, cts)
                    .ConfigureAwait(false);

                if (onePassResult.Success)
                {
                    return onePassResult;
                }
            }

            ThumbnailCreateResult perPanelResult = await TryCreatePerPanelSeekAsync(
                ctx,
                thumbInfo,
                ffmpegExePath,
                cts).ConfigureAwait(false);

            if (perPanelResult.Success)
            {
                return perPanelResult;
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

            string reason = perPanelResult.FailureReason ?? "ffmpeg failed";
            if (!string.IsNullOrWhiteSpace(singleFrameResult.FailureReason))
            {
                reason = $"{reason}; single-frame: {singleFrameResult.FailureReason}";
            }

            return ThumbnailCreateResult.Failed(reason);
        }

        /// <summary>
        /// OpenCV と同様に、各パネル秒へシークして 1 枚ずつ取得し、タイル合成する。
        /// </summary>
        private static async Task<ThumbnailCreateResult> TryCreatePerPanelSeekAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
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

            DeleteOldTempPanelFiles(ctx);

            List<string> panelPaths = [];
            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            int jpegQuality = ResolveJpegQuality();
            string vf = BuildAspectFillCropFilter(targetWidth, targetHeight);
            string lastError = "per-panel extract failed";
            string decoderLabel = "";

            try
            {
                for (int i = 0; i < panelCount; i++)
                {
                    string saveFile = Path.Combine(ctx.TempPath, $"tn_{ctx.TempFileBody}{i:D2}.jpg");
                    if (File.Exists(saveFile))
                    {
                        File.Delete(saveFile);
                    }

                    string seekText = Math.Max(0, thumbInfo.ThumbSec[i])
                        .ToString("0.###", CultureInfo.InvariantCulture);
                    (bool ok, string stderr, string panelDecoder) = await RunFfmpegWithHardwareFallbackAsync(
                            ffmpegExePath,
                            hwMode => BuildExtractArgs(
                                hwMode,
                                seekText,
                                ctx.MovieFullPath,
                                jpegQuality,
                                vf,
                                saveFile),
                            PerPanelTimeout,
                            cts)
                        .ConfigureAwait(false);

                    if (!ok || !File.Exists(saveFile))
                    {
                        lastError = string.IsNullOrWhiteSpace(stderr) ? lastError : stderr;
                        return ThumbnailCreateResult.Failed(lastError, "FFmpeg", panelDecoder);
                    }

                    if (string.IsNullOrEmpty(decoderLabel))
                    {
                        decoderLabel = panelDecoder;
                    }

                    panelPaths.Add(saveFile);
                }

                using Bitmap bmp = ConcatImages(panelPaths, cols, rows);
                if (bmp == null)
                {
                    return ThumbnailCreateResult.Failed("ffmpeg per-panel concat failed");
                }

                if (File.Exists(ctx.SaveThumbFileName))
                {
                    File.Delete(ctx.SaveThumbFileName);
                }

                bmp.Save(ctx.SaveThumbFileName, ImageFormat.Jpeg);
                ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
                return ThumbnailCreateResult.Succeeded(panelPaths, "FFmpeg", decoderLabel);
            }
            catch (Exception ex)
            {
                return ThumbnailCreateResult.Failed(ex.Message);
            }
            finally
            {
                CleanupTempPanelFiles(ctx);
            }
        }

        /// <summary>
        /// パネル個別取得に失敗したファイル向け。1 フレームだけ抽出し、同一画像を並べてサムネを構成する。
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
            string decoderLabel = "";

            foreach (double seekSec in BuildSingleFrameSeekPoints(durationSec, thumbInfo))
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }

                string seekText = seekSec.ToString("0.###", CultureInfo.InvariantCulture);
                (bool ok, string stderr, string frameDecoder) = await RunFfmpegWithHardwareFallbackAsync(
                        ffmpegExePath,
                        hwMode => BuildExtractArgs(
                            hwMode,
                            seekText,
                            ctx.MovieFullPath,
                            jpegQuality,
                            BuildAspectFillCropFilter(targetWidth, targetHeight),
                            tempFile),
                        PerPanelTimeout,
                        cts)
                    .ConfigureAwait(false);

                if (!ok || !File.Exists(tempFile))
                {
                    lastError = string.IsNullOrWhiteSpace(stderr) ? lastError : stderr;
                    continue;
                }

                if (string.IsNullOrEmpty(decoderLabel))
                {
                    decoderLabel = frameDecoder;
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
                    return ThumbnailCreateResult.Succeeded([ctx.SaveThumbFileName], "FFmpeg", decoderLabel);
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

            return ThumbnailCreateResult.Failed(lastError, "FFmpeg", decoderLabel);
        }

        private static IEnumerable<double> BuildSingleFrameSeekPoints(double durationSec, ThumbInfo thumbInfo)
        {
            LinkedList<double> points = [];
            double maxSeekSec = ThumbnailSamplingPolicy.GetEffectiveSamplingDuration(durationSec, isManual: false);
            if (maxSeekSec <= 0d)
            {
                maxSeekSec = ThumbnailSamplingPolicy.UnknownDurationSeekWindowSec;
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
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffmpeg hwdecode {FfmpegHardwareDecodePolicy.GetHwaccelName(mode)} failed: {stderr}"
                );
            }

            (bool softwareOk, string softwareStderr) = await FfmpegProcessRunner
                .RunAsync(ffmpegExePath, buildArgs(null), timeout, cts)
                .ConfigureAwait(false);

            return (softwareOk, softwareStderr, "software");
        }

        private static List<string> BuildExtractArgs(
            FfmpegHardwareDecodeMode? hwMode,
            string seekText,
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
            args.Add(seekText);
            args.Add("-i");
            args.Add(inputPath);
            args.Add("-frames:v");
            args.Add("1");
            args.Add("-pix_fmt");
            args.Add("yuv420p");
            args.Add("-q:v");
            args.Add(jpegQuality.ToString(CultureInfo.InvariantCulture));
            args.Add("-vf");
            args.Add(vf);
            args.Add(outputPath);
            return args;
        }

        private static string BuildAspectFillCropFilter(int width, int height)
        {
            return
                $"scale={width}:{height}:force_original_aspect_ratio=increase:flags=lanczos,"
                + $"crop={width}:{height},setsar=1";
        }

        private static void DeleteOldTempPanelFiles(ThumbnailJobContext ctx)
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

        private static void CleanupTempPanelFiles(ThumbnailJobContext ctx)
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

        private static bool IsBenchPerPanelOnly()
        {
            string raw = Environment.GetEnvironmentVariable(BenchPerPanelOnlyEnvName)?.Trim() ?? "";
            return raw is "1" or "true" or "on" or "yes";
        }
    }
}
