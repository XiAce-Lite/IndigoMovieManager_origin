using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using IndigoMovieManager.Thumbnail;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

public class ZipDetailThumbnailMaterializerTests
{
    private static void WriteCompositeThumb(string path, int width = 160, int height = 120)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using Bitmap bitmap = new(width, height);
        bitmap.Save(path, ImageFormat.Jpeg);

        var thumbInfo = new ThumbInfo
        {
            ThumbWidth = width,
            ThumbHeight = height,
            ThumbRows = 1,
            ThumbColumns = 1,
            ThumbCounts = 1,
        };
        thumbInfo.Add(0);
        thumbInfo.NewThumbInfo();
        ThumbnailMetadataWriter.AppendMetadata(path, thumbInfo);
    }

    [Fact]
    public void TryCopyFile_copies_existing_thumb_to_detail_path()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "imm-zip-detail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            string source = Path.Combine(tempDir, "source.jpg");
            string detail = Path.Combine(tempDir, "detail", "target.jpg");
            File.WriteAllText(source, "thumb");

            Assert.True(ZipDetailThumbnailMaterializer.TryCopyFile(source, detail));
            Assert.True(File.Exists(detail));
            Assert.Equal("thumb", File.ReadAllText(detail));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void TryCopyFromExistingTabThumbs_uses_layout_cache_paths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "imm-zip-detail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", tempDir, tabCount: 5);

            string movieBody = "sample";
            string hash = "abc123";
            string sourcePath = cache.GetExpectedThumbPath(2, movieBody, hash);
            string detailPath = cache.GetExpectedThumbPath(99, movieBody, hash);

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            WriteCompositeThumb(sourcePath);

            Assert.True(ZipDetailThumbnailMaterializer.TryCopyFromExistingTabThumbs(
                cache,
                movieBody,
                hash,
                detailPath));
            Assert.True(File.Exists(detailPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
