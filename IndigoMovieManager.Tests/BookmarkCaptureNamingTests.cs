using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class BookmarkCaptureNamingTests
{
    [Fact]
    public void BuildThumbBody_uses_frame_and_timestamp()
    {
        DateTime stamp = new(2026, 7, 30, 14, 5, 9);
        string body = BookmarkCaptureNaming.BuildThumbBody("abcd-123", 10, 29.97d, stamp);

        Assert.Equal("abcd-123[(290)14-05-09]", body);
    }

    [Fact]
    public void BuildThumbFilePath_joins_folder_and_jpg_name()
    {
        string path = BookmarkCaptureNaming.BuildThumbFilePath(@"C:\bookmark", "abcd-123[(290)14-05-09]");
        Assert.Equal(@"C:\bookmark\abcd-123[(290)14-05-09].jpg", path);
        Assert.Equal("abcd-123[(290)14-05-09].jpg", BookmarkCaptureNaming.BuildThumbFileName("abcd-123[(290)14-05-09]"));
    }

    [Fact]
    public void ResolveFolderOrDefault_falls_back_to_local_bookmark_folder()
    {
        string resolved = BookmarkCaptureNaming.ResolveFolderOrDefault("", "sample-db");
        Assert.EndsWith(Path.Combine("bookmark", "sample-db"), resolved);
    }
}

public class ManualThumbnailCaptureFactoryTests
{
    [Fact]
    public void Create_sets_manual_capture_fields()
    {
        QueueObj item = ManualThumbnailCaptureFactory.Create(12, @"D:\movies\abcd-123.mp4", 3, 45);

        Assert.Equal(12, item.MovieId);
        Assert.Equal(@"D:\movies\abcd-123.mp4", item.MovieFullPath);
        Assert.Equal(3, item.ThumbPanelPos);
        Assert.Equal(45, item.ThumbTimePos);
        Assert.True(item.IsManual);
    }
}
