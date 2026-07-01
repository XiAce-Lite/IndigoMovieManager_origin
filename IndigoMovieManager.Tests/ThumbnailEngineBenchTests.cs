using System.Diagnostics;
using IndigoMovieManager;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;
using Xunit;

namespace IndigoMovieManager.Tests;

/// <summary>
/// 実ファイルでのエンジン比較ベンチ。IMM_THUMB_BENCH_VIDEOS にセミコロン区切りで動画パスを指定。
/// 結果は Debug 出力（イミディエイトウィンドウ）に出る。
/// </summary>
public sealed class ThumbnailEngineBenchTests
{
    [Fact]
    public async Task Compare_opencv_perpanel_ffmpeg_and_onepass()
    {
        string fromEnv = Environment.GetEnvironmentVariable("IMM_THUMB_BENCH_VIDEOS")?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fromEnv))
        {
            return;
        }

        if (!FfmpegPathResolver.TryResolve(out string ffmpegPath))
        {
            return;
        }

        foreach (string videoPath in fromEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!File.Exists(videoPath))
            {
                continue;
            }

            await BenchOneVideoAsync(videoPath, ffmpegPath);
        }
    }

    private static async Task BenchOneVideoAsync(string videoPath, string ffmpegPath)
    {
        const int cols = 3;
        const int rows = 3;
        const int width = 220;
        const int height = 124;

        string workDir = Path.Combine(Path.GetTempPath(), "imm-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            if (!ThumbnailDurationResolver.TryResolve(videoPath, out double durationSec, knownDurationSec: 0)
                || durationSec <= 0)
            {
                return;
            }

            int divideSec = (int)(durationSec / (cols * rows + 1));
            if (divideSec < 1)
            {
                divideSec = 1;
            }

            var thumbInfo = new ThumbInfo
            {
                ThumbWidth = width,
                ThumbHeight = height,
                ThumbColumns = cols,
                ThumbRows = rows,
                ThumbCounts = cols * rows,
            };
            for (int i = 1; i < cols * rows + 1; i++)
            {
                thumbInfo.Add(i * divideSec);
            }
            thumbInfo.NewThumbInfo();

            var layout = new ThumbnailLayoutSpec(width, height, cols, rows);
            var tabInfo = new TabInfo(layout, "bench", workDir);

            ThumbnailJobContext MakeCtx(string outName) => new()
            {
                MovieFullPath = videoPath,
                SaveThumbFileName = Path.Combine(workDir, outName),
                TempFileBody = "bench_temp",
                TempPath = workDir,
                TabInfo = tabInfo,
            };

            long opencvMs = await MeasureAsync(async () =>
            {
                ThumbnailCreateResult r = await OpenCvThumbnailCreator.TryCreateAsync(
                    MakeCtx("out_opencv.jpg"),
                    thumbInfo,
                    default);
                Assert.True(r.Success, r.FailureReason);
            });

            Environment.SetEnvironmentVariable("IMM_THUMB_BENCH_PERPANEL_ONLY", "1");
            try
            {
                long perPanelMs = await MeasureAsync(async () =>
                {
                    ThumbnailCreateResult r = await FfmpegFallbackCreator.TryCreateAsync(
                        MakeCtx("out_perpanel.jpg"),
                        thumbInfo,
                        durationSec,
                        ffmpegPath,
                        default);
                    Assert.True(r.Success, r.FailureReason);
                });

                long onePassMs = await MeasureAsync(async () =>
                {
                    ThumbnailCreateResult r = await FfmpegOnePassCreator.TryCreateAsync(
                        MakeCtx("out_onepass.jpg"),
                        thumbInfo,
                        durationSec,
                        ffmpegPath,
                        default);
                    Assert.True(r.Success, r.FailureReason);
                });

                string line =
                    $"[thumb-bench] {videoPath} duration={durationSec:0}s opencv={opencvMs}ms perpanel={perPanelMs}ms onepass={onePassMs}ms";
                Debug.WriteLine(line);
            }
            finally
            {
                Environment.SetEnvironmentVariable("IMM_THUMB_BENCH_PERPANEL_ONLY", null);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task<long> MeasureAsync(Func<Task> action)
    {
        Stopwatch sw = Stopwatch.StartNew();
        await action().ConfigureAwait(false);
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }
}
