using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DetailPaneThumbResolveTests
{
    private static void WriteCompositePlaceholder(string path)
    {
        byte[] data = new byte[128];
        BitConverter.GetBytes((ushort)1).CopyTo(data, data.Length - 60);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, data);
    }

    [Fact]
    public void ResolveDetailThumbPath_uses_detail_folder_when_present()
    {
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-detail-{Guid.NewGuid():N}");
        var cache = new ThumbnailLayoutCache();
        cache.Refresh("testdb", thumbRoot);

        try
        {
            string thumbFile = ThumbnailLayoutCache.GetThumbFileName("movie", "abc123");
            string detailPath = Path.Combine(cache.DetailPaneOutPath, thumbFile);
            WriteCompositePlaceholder(detailPath);

            string resolved = cache.ResolveDetailThumbPath(thumbFile, checkExists: true);
            Assert.Equal(detailPath, resolved);
        }
        finally
        {
            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }

    [Fact]
    public void ResolveDetailThumbPath_falls_back_to_single_panel_list_layout()
    {
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-detail-{Guid.NewGuid():N}");
        var cache = new ThumbnailLayoutCache();
        cache.Refresh("testdb", thumbRoot);
        var listLayout = new ThumbnailLayoutSpec(160, 120, 1, 1);

        try
        {
            string thumbFile = ThumbnailLayoutCache.GetThumbFileName("movie", "abc123");
            string listPath = cache.GetExpectedThumbPath(listLayout, "movie", "abc123");
            WriteCompositePlaceholder(listPath);

            string resolved = cache.ResolveDetailThumbPath(thumbFile, checkExists: true, listLayout);
            Assert.Equal(listPath, resolved);
        }
        finally
        {
            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }
}
