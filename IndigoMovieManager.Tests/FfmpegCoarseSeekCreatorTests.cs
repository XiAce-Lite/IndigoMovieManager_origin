using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class FfmpegCoarseSeekCreatorTests
{
    [Fact]
    public void BuildExtractArgs_places_ss_before_input()
    {
        List<string> args = FfmpegCoarseSeekCreator.BuildExtractArgsForTest(
            "123.5",
            @"C:\videos\sample.mp4",
            @"C:\temp\out.jpg");

        int ssIndex = args.IndexOf("-ss");
        int inputIndex = args.IndexOf("-i");
        Assert.True(ssIndex >= 0);
        Assert.True(inputIndex > ssIndex);
        Assert.Equal("123.5", args[ssIndex + 1]);
        Assert.Equal(@"C:\videos\sample.mp4", args[inputIndex + 1]);
    }

    [Fact]
    public async Task TryCreate_rejects_manual_mode()
    {
        var ctx = new ThumbnailJobContext
        {
            IsManual = true,
            MovieFullPath = @"C:\videos\sample.mp4",
            SaveThumbFileName = @"C:\temp\out.jpg",
            TempPath = Path.GetTempPath(),
            TempFileBody = "coarse_test",
            TabInfo = new TabInfo(new ThumbnailLayoutSpec(160, 120, 2, 2), "db", Path.GetTempPath()),
        };
        var thumbInfo = new ThumbInfo { ThumbCounts = 4 };
        thumbInfo.Add(10);
        thumbInfo.Add(20);
        thumbInfo.Add(30);
        thumbInfo.Add(40);

        ThumbnailCreateResult result = await FfmpegCoarseSeekCreator.TryCreateAsync(
            ctx,
            thumbInfo,
            @"C:\ffmpeg\ffmpeg.exe",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("manual", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    public void IsAutoCoarseSeekPreferred_false_when_div_count_exceeds_threshold(int divCount)
    {
        Assert.False(FfmpegPathResolver.IsAutoCoarseSeekPreferred(divCount));
    }

    [Fact]
    public void AutoCoarseSeekMaxDivCount_is_four()
    {
        Assert.Equal(4, FfmpegPathResolver.AutoCoarseSeekMaxDivCount);
    }

    [Fact]
    public void IsAutoCoarseSeekPreferred_respects_opencv_override()
    {
        string previous = Environment.GetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE");
        try
        {
            Environment.SetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE", "opencv");
            Assert.False(FfmpegPathResolver.IsAutoCoarseSeekPreferred(divCount: 3));
            Assert.False(FfmpegPathResolver.IsAutoCoarseSeekPreferred(divCount: 5));
        }
        finally
        {
            Environment.SetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE", previous);
        }
    }
}
