using System.IO.Compression;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ZipSamplingPolicyTests
{
    [Theory]
    [InlineData(10, 4, new[] { 1, 3, 5, 7 })]
    [InlineData(1, 4, new[] { 0, 0, 0, 0 })]
    [InlineData(3, 1, new[] { 1 })]
    public void PickIndices_uses_k_over_n_plus_one(int imageCount, int panelCount, int[] expected)
    {
        int[] actual = ZipSamplingPolicy.PickIndices(imageCount, panelCount);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PickIndices_returns_empty_when_invalid()
    {
        Assert.Empty(ZipSamplingPolicy.PickIndices(0, 4));
        Assert.Empty(ZipSamplingPolicy.PickIndices(4, 0));
    }
}

public class ZipImageCatalogTests
{
    [Fact]
    public void TryGetImageEntries_lists_images_in_ordinal_ignore_case_order()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-zip-{Guid.NewGuid():N}.zip");
        try
        {
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                archive.CreateEntry("folder/b.png");
                archive.CreateEntry("a.jpg");
                archive.CreateEntry("notes.txt");
                archive.CreateEntry("root.gif");
            }

            Assert.True(ZipImageCatalog.TryGetImageEntries(zipPath, out IReadOnlyList<string> entries));
            Assert.Equal(["a.jpg", "folder/b.png", "root.gif"], entries);
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
    public void TryExtractEntry_writes_selected_image()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-zip-{Guid.NewGuid():N}.zip");
        string destPath = Path.Combine(Path.GetTempPath(), $"imm-zip-out-{Guid.NewGuid():N}.png");
        byte[] payload = [0x89, 0x50, 0x4E, 0x47];
        try
        {
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                ZipArchiveEntry entry = archive.CreateEntry("first.png");
                using (Stream entryStream = entry.Open())
                {
                    entryStream.Write(payload, 0, payload.Length);
                }

                archive.CreateEntry("second.jpg");
            }

            Assert.True(ZipImageCatalog.TryExtractEntry(zipPath, 0, destPath));
            Assert.True(File.Exists(destPath));
            Assert.Equal(payload, File.ReadAllBytes(destPath));
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
        }
    }
}

public class MovieInfoZipTests
{
    [Fact]
    public void Constructor_sets_zip_container_and_image_count()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-zip-{Guid.NewGuid():N}.zip");
        try
        {
            using (FileStream stream = File.Create(zipPath))
            using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                archive.CreateEntry("1.jpg");
                archive.CreateEntry("2.png");
                archive.CreateEntry("readme.txt");
            }

            MovieInfo info = new(zipPath);
            Assert.Equal("zip", info.Container);
            Assert.Equal(2, info.MovieLength);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }
}
