using System.Drawing;
using System.IO;
using IndigoMovieManager;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using OpenCvSharp;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

[CollectionDefinition("ThumbnailLayoutGeneration", DisableParallelization = true)]
public sealed class ThumbnailLayoutGenerationCollection;

/// <summary>
/// WPF スキン / TabInfo で定義した W×H×C×R に従いサムネイルが生成されることを検証する。
/// </summary>
[Collection("ThumbnailLayoutGeneration")]
public class ThumbnailLayoutGenerationTests
{
    [Theory]
    [InlineData(400, 225, 1, 1)]
    [InlineData(360, 203, 3, 1)]
    [InlineData(120, 90, 3, 1)]
    public async Task CreateAsync_respects_layout_spec_in_pixels_and_metadata(
        int panelWidth,
        int panelHeight,
        int columns,
        int rows)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "imm-thumb-layout-" + Guid.NewGuid().ToString("N"));
        string videoPath = Path.Combine(workDir, "sample.mp4");
        string tempPath = Path.Combine(workDir, "temp");
        string thumbRoot = Path.Combine(workDir, "thumb");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(thumbRoot);

        try
        {
            videoPath = await EnsureTestVideoAsync(workDir);
            if (string.IsNullOrEmpty(videoPath))
            {
                return;
            }

            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            var spec = new ThumbnailLayoutSpec(panelWidth, panelHeight, columns, rows);
            var tabInfo = new TabInfo(spec, "testdb", thumbRoot);
            string fileBody = "sample";
            string hash = GetHashCRC32(videoPath);
            string saveThumb = cache.GetExpectedThumbPath(spec, fileBody, hash);
            Directory.CreateDirectory(tabInfo.OutPath);

            var movie = new MovieRecords
            {
                Movie_Id = 1,
                Movie_Path = videoPath,
                Movie_Name = Path.GetFileName(videoPath),
                Hash = hash,
            };

            var queueObj = new QueueObj
            {
                MovieId = movie.Movie_Id,
                MovieFullPath = videoPath,
                ThumbnailLayout = spec,
                DbFullPath = Path.Combine(workDir, "test.db"),
                WorkGeneration = 1,
            };

            var host = new ThumbnailCreationHost
            {
                DbFullPath = queueObj.DbFullPath,
                DbName = "testdb",
                ThumbFolder = thumbRoot,
                LayoutCache = cache,
                RunOnUi = action => action(),
                ApplyThumbPathsOnUi = (_, _) => { },
                ApplyFailurePlaceholder = (_, _) => { },
                UpdateMovieColumn = (_, _, _) => { },
                IsSessionActive = () => true,
                FindMovieRecord = id => id == movie.Movie_Id ? movie : null,
            };

            await ThumbnailCreationOrchestrator.CreateAsync(host, queueObj);

            Assert.True(File.Exists(saveThumb), "thumbnail file was not created");
            Assert.True(ThumbnailValidityHelper.LooksLikeCompositeThumbnail(saveThumb));

            int expectedImageWidth = panelWidth * columns;
            int expectedImageHeight = panelHeight * rows;

            using (var bitmap = Image.FromFile(saveThumb))
            {
                Assert.Equal(expectedImageWidth, bitmap.Width);
                Assert.Equal(expectedImageHeight, bitmap.Height);
            }

            var meta = new ThumbInfo();
            meta.GetThumbInfo(saveThumb);
            Assert.True(meta.IsThumbnail);
            Assert.Equal(panelWidth, meta.ThumbWidth);
            Assert.Equal(panelHeight, meta.ThumbHeight);
            Assert.Equal(columns, meta.ThumbColumns);
            Assert.Equal(rows, meta.ThumbRows);
            Assert.Equal(columns * rows, meta.ThumbCounts);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    [Fact]
    public void FitFrameToPanel_produces_exact_panel_dimensions()
    {
        using Mat source = new(1080, 1920, MatType.CV_8UC3, new Scalar(40, 120, 200));
        using Mat panel = ThumbnailImageGeometry.FitFrameToPanel(source, 400, 225);

        Assert.False(panel.Empty());
        Assert.Equal(400, panel.Width);
        Assert.Equal(225, panel.Height);
    }

    [Fact]
    public void FitFrameToPanel_crops_off_aspect_source_to_fill()
    {
        // 4:3 ソースを 16:9 パネルへ → 黒帯を出さずセンタークロップで全面充填する
        using Mat source = new(480, 640, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using Mat panel = ThumbnailImageGeometry.FitFrameToPanel(source, 400, 225);

        Assert.Equal(400, panel.Width);
        Assert.Equal(225, panel.Height);

        // 上端・中央いずれも黒帯ではなく元色（赤）で埋まっていること
        Vec3b top = panel.At<Vec3b>(2, 200);
        Vec3b center = panel.At<Vec3b>(112, 200);
        Assert.True(top.Item2 > 200);
        Assert.True(center.Item2 > 200);
    }

    [Fact]
    public void FitFrameToPanel_keeps_four_three_panel_filled_for_widescreen_source()
    {
        // 16:9 ソースを List 互換の 4:3 パネル（56x42）へ → 上下に黒帯を作らない
        using Mat source = new(1080, 1920, MatType.CV_8UC3, new Scalar(0, 0, 255));
        using Mat panel = ThumbnailImageGeometry.FitFrameToPanel(source, 56, 42);

        Assert.Equal(56, panel.Width);
        Assert.Equal(42, panel.Height);

        Vec3b top = panel.At<Vec3b>(1, 28);
        Vec3b bottom = panel.At<Vec3b>(40, 28);
        Assert.True(top.Item2 > 200);
        Assert.True(bottom.Item2 > 200);
    }

    private static async Task<string> EnsureTestVideoAsync(string workDir)
    {
        string mp4Path = Path.Combine(workDir, "sample.mp4");
        string ffmpegExe = ResolveBundledFfmpeg();
        if (ffmpegExe != null && await CreateTestVideoAsync(ffmpegExe, mp4Path).ConfigureAwait(true))
        {
            return mp4Path;
        }

        string aviPath = Path.Combine(workDir, "sample.avi");
        return TryCreateTestVideoWithOpenCv(aviPath) ? aviPath : null;
    }

    private static bool TryCreateTestVideoWithOpenCv(string destPath)
    {
        try
        {
            const int width = 640;
            const int height = 360;
            const int fps = 10;
            const int frameCount = 300;

            using var writer = new VideoWriter(destPath, FourCC.FromString("MJPG"), fps, new OpenCvSharp.Size(width, height));
            if (!writer.IsOpened())
            {
                return false;
            }

            using Mat frame = new(height, width, MatType.CV_8UC3);
            for (int i = 0; i < frameCount; i++)
            {
                // パネルごとに色が変わるようフレームを変化させ、B-6 重複検出で落ちないようにする。
                frame.SetTo(new Scalar((i * 17) % 256, (i * 29) % 256, (i * 43) % 256));
                writer.Write(frame);
            }

            writer.Release();
            return File.Exists(destPath) && new FileInfo(destPath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CreateTestVideoAsync(string ffmpegExe, string destPath)
    {
        List<string> args =
        [
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc=duration=30:size=1920x1080:rate=30",
            "-pix_fmt", "yuv420p",
            destPath,
        ];

        (bool ok, _) = await FfmpegProcessRunner
            .RunAsync(ffmpegExe, args, TimeSpan.FromSeconds(60), CancellationToken.None)
            .ConfigureAwait(false);
        return ok && File.Exists(destPath);
    }

    private static string ResolveBundledFfmpeg()
    {
        string fromEnv = Environment.GetEnvironmentVariable("IMM_FFMPEG_EXE_PATH")?.Trim() ?? "";
        if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe"),
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string repoTools = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", "tools", "ffmpeg", "ffmpeg.exe"));
        if (File.Exists(repoTools))
        {
            return repoTools;
        }

        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = dir.Trim().Trim('"');
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            string candidate = Path.Combine(trimmed, "ffmpeg.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
