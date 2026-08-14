using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 自動サムネ用: 各パネルを入力前 <c>-ss</c> で独立シークして抽出する（キーフレーム近似でよい）。
    /// 長尺動画でも OpenCV の前方デコードより速く、1pass のデコード幅上限にも縛られない。
    /// 手動サムネでは使わない（フレーム精度が必要なため）。
    /// </summary>
    internal static class FfmpegCoarseSeekCreator
    {
        private const string JpegQualityEnvName = "IMM_THUMB_JPEG_Q";
        private const int DefaultJpegQuality = 5;
        private static readonly TimeSpan PerPanelTimeout = TimeSpan.FromSeconds(90);

        /// <summary>
        /// ジョブ横断で同時に動く ffmpeg パネル抽出の上限（設定の並列度を流用）。
        /// </summary>
        private static readonly object GateSync = new();
        private static SemaphoreSlim _panelExtractGate = CreateGate(4);
        private static int _panelExtractGateSize = 4;

        public static async Task<ThumbnailCreateResult> TryCreateAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
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
                return ThumbnailCreateResult.Failed("manual mode requires precise seek");
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

            DeleteOldTempPanelFiles(ctx);

            string[] panelPaths = new string[panelCount];
            (int targetWidth, int targetHeight) = ResolveTargetSize(ctx);
            int jpegQuality = ResolveJpegQuality();
            string vf = BuildAspectFillCropFilter(targetWidth, targetHeight);
            string lastError = "per-panel extract failed";
            string decoderLabel = "";
            object decoderLock = new();
            SemaphoreSlim gate = GetPanelExtractGate();

            try
            {
                Task[] tasks = new Task[panelCount];
                for (int i = 0; i < panelCount; i++)
                {
                    int index = i;
                    tasks[index] = ExtractPanelAsync(
                        index,
                        ctx,
                        thumbInfo,
                        ffmpegExePath,
                        jpegQuality,
                        vf,
                        panelPaths,
                        gate,
                        decoderLock,
                        () => decoderLabel,
                        label => decoderLabel = label,
                        err => lastError = err,
                        cts);
                }

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return ThumbnailCreateResult.Failed(lastError, "FFmpeg", decoderLabel);
                }

                List<string> orderedPaths = [.. panelPaths];
                using Bitmap bmp = ConcatImages(orderedPaths, cols, rows);
                if (bmp == null)
                {
                    return ThumbnailCreateResult.Failed("ffmpeg coarse-seek concat failed");
                }

                if (File.Exists(ctx.SaveThumbFileName))
                {
                    File.Delete(ctx.SaveThumbFileName);
                }

                bmp.Save(ctx.SaveThumbFileName, ImageFormat.Jpeg);
                ThumbnailMetadataWriter.AppendMetadata(ctx.SaveThumbFileName, thumbInfo);
                return ThumbnailCreateResult.Succeeded(orderedPaths, "FFmpeg", decoderLabel);
            }
            catch (OperationCanceledException)
            {
                throw;
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

        /// <summary>テスト用: <c>-ss</c> が <c>-i</c> より前に来ることを検証する。</summary>
        internal static List<string> BuildExtractArgsForTest(
            string seekText,
            string inputPath,
            string outputPath) =>
            BuildExtractArgs(null, seekText, inputPath, DefaultJpegQuality, "null", outputPath);

        private static async Task ExtractPanelAsync(
            int index,
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            string ffmpegExePath,
            int jpegQuality,
            string vf,
            string[] panelPaths,
            SemaphoreSlim gate,
            object decoderLock,
            Func<string> getDecoderLabel,
            Action<string> setDecoderLabel,
            Action<string> setLastError,
            CancellationToken cts)
        {
            await gate.WaitAsync(cts).ConfigureAwait(false);
            try
            {
                cts.ThrowIfCancellationRequested();

                string saveFile = Path.Combine(ctx.TempPath, $"tn_{ctx.TempFileBody}{index:D2}.jpg");
                if (File.Exists(saveFile))
                {
                    File.Delete(saveFile);
                }

                string seekText = Math.Max(0, thumbInfo.ThumbSec[index])
                    .ToString("0.###", CultureInfo.InvariantCulture);
                (bool ok, string stderr, string panelDecoder) =
                    await RunFfmpegWithHardwareFallbackAsync(
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
                    string err = string.IsNullOrWhiteSpace(stderr)
                        ? "per-panel extract failed"
                        : stderr;
                    lock (decoderLock)
                    {
                        setLastError(err);
                        if (string.IsNullOrEmpty(getDecoderLabel()))
                        {
                            setDecoderLabel(panelDecoder);
                        }
                    }

                    throw new InvalidOperationException(err);
                }

                lock (decoderLock)
                {
                    if (string.IsNullOrEmpty(getDecoderLabel()))
                    {
                        setDecoderLabel(panelDecoder);
                    }
                }

                panelPaths[index] = saveFile;
            }
            finally
            {
                gate.Release();
            }
        }

        private static SemaphoreSlim GetPanelExtractGate()
        {
            int desired = ThumbnailQueueProcessor.ClampThumbnailParallelism(
                Properties.Settings.Default.ThumbnailParallelism);
            if (desired < 1)
            {
                desired = 1;
            }

            lock (GateSync)
            {
                if (desired == _panelExtractGateSize)
                {
                    return _panelExtractGate;
                }

                // 進行中の Wait を壊さないよう、容量変更時は新ゲートを差し替える（旧は GC 待ち）。
                _panelExtractGate = CreateGate(desired);
                _panelExtractGateSize = desired;
                return _panelExtractGate;
            }
        }

        private static SemaphoreSlim CreateGate(int size) => new(size, size);

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

            // 入力前 -ss: キーフレーム近似でよい独立シーク（長尺向け）。
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
            CleanupTempPanelFiles(ctx);
        }

        private static void CleanupTempPanelFiles(ThumbnailJobContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.TempPath) || !Directory.Exists(ctx.TempPath))
            {
                return;
            }

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
    }
}
