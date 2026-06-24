using IndigoMovieManager;
using IndigoMovieManager.Thumbnail;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

public class ZipRealFileDiagnosticTests
{
    public static TheoryData<string> RealZipPaths => new()
    {
        @"J:\Comics\Gravure\FLASH_2026-05-12-19.zip",
        @"J:\Comics\Gravure\FLASH_2026-06-23-30.zip",
    };

    [Theory]
    [MemberData(nameof(RealZipPaths))]
    public void ZipImageCatalog_lists_webp_images(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            return;
        }

        Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.EndsWith(".webp", e, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(RealZipPaths))]
    public async Task ZipThumbnailCreator_creates_three_panel_thumbnail(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            return;
        }

        Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));

        string workDir = Path.Combine(Path.GetTempPath(), "imm-realzip3-" + Guid.NewGuid().ToString("N"));
        string tempPath = Path.Combine(workDir, "temp");
        string thumbRoot = Path.Combine(workDir, "thumb");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(thumbRoot);

        try
        {
            TabInfo tabInfo = new(0, "testdb", thumbRoot);
            string saveThumb = Path.Combine(tabInfo.OutPath, "sample.#hash.jpg");
            Directory.CreateDirectory(tabInfo.OutPath);

            int[] indices = ZipSamplingPolicy.PickIndices(entries.Count, tabInfo.Columns * tabInfo.Rows);
            var thumbInfo = new ThumbInfo
            {
                ThumbWidth = tabInfo.Width,
                ThumbHeight = tabInfo.Height,
                ThumbRows = tabInfo.Rows,
                ThumbColumns = tabInfo.Columns,
                ThumbCounts = tabInfo.Columns * tabInfo.Rows,
            };
            foreach (int index in indices)
            {
                thumbInfo.Add(index);
            }
            thumbInfo.NewThumbInfo();

            var ctx = new ThumbnailJobContext
            {
                MovieFullPath = zipPath,
                SaveThumbFileName = saveThumb,
                TempFileBody = "realzip3_temp",
                TempPath = tempPath,
                TabInfo = tabInfo,
                IsResizeThumb = true,
            };

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

    [Theory]
    [MemberData(nameof(RealZipPaths))]
    public async Task ZipThumbnailCreator_creates_composite_thumbnail(string zipPath)
    {
        if (!File.Exists(zipPath))
        {
            return;
        }

        Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));

        string workDir = Path.Combine(Path.GetTempPath(), "imm-realzip-" + Guid.NewGuid().ToString("N"));
        string tempPath = Path.Combine(workDir, "temp");
        string thumbRoot = Path.Combine(workDir, "thumb");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(thumbRoot);

        try
        {
            TabInfo tabInfo = new(2, "testdb", thumbRoot);
            string saveThumb = Path.Combine(tabInfo.OutPath, "sample.#hash.jpg");
            Directory.CreateDirectory(tabInfo.OutPath);

            var ctx = new ThumbnailJobContext
            {
                MovieFullPath = zipPath,
                SaveThumbFileName = saveThumb,
                TempFileBody = "realzip_temp",
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
}
