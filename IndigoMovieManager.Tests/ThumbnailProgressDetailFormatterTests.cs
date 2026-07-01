using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class ThumbnailProgressDetailFormatterTests
{
    [Fact]
    public void Format_includes_full_path_backend_and_codec()
    {
        var result = ThumbnailCreateResult.Succeeded([], "OpenCV", "OpenCV");

        string detail = ThumbnailProgressDetailFormatter.Format(
            @"D:\Movies\sample.mp4",
            result,
            "H.264");

        Assert.Equal(@"D:\Movies\sample.mp4  |  OpenCV  |  H.264", detail);
    }

    [Fact]
    public void Format_shows_ffmpeg_hwaccel_label()
    {
        var result = ThumbnailCreateResult.Succeeded([], "FFmpeg", "cuda");

        string detail = ThumbnailProgressDetailFormatter.Format(
            @"D:\Movies\sample.mp4",
            result,
            "HEVC");

        Assert.Equal(@"D:\Movies\sample.mp4  |  FFmpeg (cuda)  |  HEVC", detail);
    }

    [Fact]
    public void Format_shows_ffmpeg_software_when_decoder_is_software()
    {
        var result = ThumbnailCreateResult.Succeeded([], "FFmpeg", "software");

        string detail = ThumbnailProgressDetailFormatter.Format(
            @"D:\Movies\sample.mp4",
            result,
            "H.264");

        Assert.Equal(@"D:\Movies\sample.mp4  |  FFmpeg (software)  |  H.264", detail);
    }

    [Fact]
    public void Format_path_only_when_no_backend_or_codec()
    {
        string detail = ThumbnailProgressDetailFormatter.Format(
            @"D:\Movies\sample.mp4",
            null,
            null);

        Assert.Equal(@"D:\Movies\sample.mp4", detail);
    }
}
