using System.Diagnostics;
using System.IO.Compression;
using IndigoMovieManager;
using IndigoMovieManager.Thumbnail;
using OpenCvSharp;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

public class ZipThumbnailCreatorTests
{
    [Fact]
    public void IsWebpEntry_detects_webp_extension()
    {
        Assert.True(ZipImageCatalog.IsWebpEntry("folder/page.webp"));
        Assert.True(ZipImageCatalog.IsWebpEntry("PAGE.WEBP"));
        Assert.False(ZipImageCatalog.IsWebpEntry("page.jpg"));
        Assert.False(ZipImageCatalog.IsWebpEntry("webp.txt"));
    }

    [Fact]
    public void FindEntry_matches_case_insensitive_full_name()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-zip-{Guid.NewGuid():N}.zip");
        try
        {
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                archive.CreateEntry("Folder/Photo.WEBP");
            }

            using ZipArchive readArchive = ZipFile.OpenRead(zipPath);
            ZipArchiveEntry entry = ZipArchiveEntryResolver.FindEntry(readArchive, "folder/photo.webp");
            Assert.NotNull(entry);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public async Task TryCreateAsync_decodes_webp_entry_via_opencv()
    {
        string workDir = Path.Combine(Path.GetTempPath(), "imm-zip-webp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        string zipPath = Path.Combine(workDir, "sample.zip");
        string tempPath = Path.Combine(workDir, "temp");
        string thumbRoot = Path.Combine(workDir, "thumb");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(thumbRoot);

        try
        {
            string webpBytesPath = Path.Combine(workDir, "frame.webp");
            using (Mat mat = new(32, 48, MatType.CV_8UC3, new Scalar(20, 180, 240)))
            {
                Assert.True(Cv2.ImWrite(webpBytesPath, mat));
            }

            byte[] webpData = File.ReadAllBytes(webpBytesPath);
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                ZipArchiveEntry entry = archive.CreateEntry("shots/frame.webp");
                using Stream entryStream = entry.Open();
                entryStream.Write(webpData, 0, webpData.Length);
            }

            Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));
            Assert.Single(entries);

            TabInfo tabInfo = new(2, "testdb", thumbRoot);
            string saveThumb = Path.Combine(tabInfo.OutPath, "sample.#hash.jpg");
            Directory.CreateDirectory(tabInfo.OutPath);

            var ctx = new ThumbnailJobContext
            {
                MovieFullPath = zipPath,
                SaveThumbFileName = saveThumb,
                TempFileBody = "sample_hash_tab2_temp",
                TempPath = tempPath,
                TabInfo = tabInfo,
                IsResizeThumb = true,
            };

            var thumbInfo = new ThumbInfo
            {
                ThumbWidth = tabInfo.Width,
                ThumbHeight = tabInfo.Height,
                ThumbRows = tabInfo.Rows,
                ThumbColumns = tabInfo.Columns,
                ThumbCounts = 1,
            };
            thumbInfo.Add(0);
            thumbInfo.NewThumbInfo();

            ThumbnailCreateResult result = await ZipThumbnailCreator.TryCreateAsync(ctx, thumbInfo, entries);
            Assert.True(result.Success, result.FailureReason);
            Assert.True(ThumbnailValidityHelper.LooksLikeCompositeThumbnail(saveThumb));
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
    public async Task TryCreateAsync_decodes_ffmpeg_webp_entry()
    {
        string ffmpegExe = ResolveBundledFfmpeg();
        if (ffmpegExe == null)
        {
            return;
        }

        string workDir = Path.Combine(Path.GetTempPath(), "imm-zip-ffwebp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        string zipPath = Path.Combine(workDir, "sample.zip");
        string tempPath = Path.Combine(workDir, "temp");
        string thumbRoot = Path.Combine(workDir, "thumb");
        string webpPath = Path.Combine(workDir, "frame.webp");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(thumbRoot);

        try
        {
            List<string> ffmpegArgs =
            [
                "-y", "-hide_banner", "-loglevel", "error",
                "-f", "lavfi", "-i", "color=c=red:s=80x60",
                "-frames:v", "1",
                webpPath,
            ];
            (bool created, _) = await FfmpegProcessRunner
                .RunAsync(ffmpegExe, ffmpegArgs, TimeSpan.FromSeconds(30), CancellationToken.None);
            Assert.True(created && File.Exists(webpPath));

            byte[] webpData = await File.ReadAllBytesAsync(webpPath);
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                ZipArchiveEntry entry = archive.CreateEntry("shots/frame.webp");
                using Stream entryStream = entry.Open();
                await entryStream.WriteAsync(webpData);
            }

            Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));
            Assert.Single(entries);

            TabInfo tabInfo = new(2, "testdb", thumbRoot);
            string saveThumb = Path.Combine(tabInfo.OutPath, "sample.#hash.jpg");
            Directory.CreateDirectory(tabInfo.OutPath);

            var ctx = new ThumbnailJobContext
            {
                MovieFullPath = zipPath,
                SaveThumbFileName = saveThumb,
                TempFileBody = "sample_hash_tab2_temp",
                TempPath = tempPath,
                TabInfo = tabInfo,
                IsResizeThumb = true,
            };

            var thumbInfo = new ThumbInfo
            {
                ThumbWidth = tabInfo.Width,
                ThumbHeight = tabInfo.Height,
                ThumbRows = tabInfo.Rows,
                ThumbColumns = tabInfo.Columns,
                ThumbCounts = 1,
            };
            thumbInfo.Add(0);
            thumbInfo.NewThumbInfo();

            ThumbnailCreateResult result = await ZipThumbnailCreator.TryCreateAsync(ctx, thumbInfo, entries);
            Assert.True(result.Success, result.FailureReason);
            Assert.True(ThumbnailValidityHelper.LooksLikeCompositeThumbnail(saveThumb));
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    private static string ResolveBundledFfmpeg()
    {
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

        string repoTools = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "tools", "ffmpeg", "ffmpeg.exe"));
        return File.Exists(repoTools) ? repoTools : null;
    }
}
