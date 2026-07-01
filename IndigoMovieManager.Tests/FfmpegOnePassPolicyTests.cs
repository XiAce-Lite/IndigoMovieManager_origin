using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class FfmpegOnePassPolicyTests
{
    [Fact]
    public void CanUse_true_for_uniform_divideSec_layout_within_decode_span_limit()
    {
        var thumbInfo = new ThumbInfo
        {
            ThumbCounts = 9,
            ThumbColumns = 3,
            ThumbRows = 3,
        };
        for (int i = 1; i <= 9; i++)
        {
            thumbInfo.Add(i * 60);
        }

        Assert.True(FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec: 600));
        Assert.Equal(60d, FfmpegOnePassPolicy.ResolveStartSec(thumbInfo));
        Assert.Equal(60d, FfmpegOnePassPolicy.ResolveIntervalSec(thumbInfo, 600));
    }

    [Fact]
    public void CanUse_false_when_decode_span_exceeds_limit()
    {
        var thumbInfo = new ThumbInfo
        {
            ThumbCounts = 9,
            ThumbColumns = 3,
            ThumbRows = 3,
        };
        for (int i = 1; i <= 9; i++)
        {
            thumbInfo.Add(i * 720);
        }

        Assert.False(FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec: 7200));
    }

    [Fact]
    public void CanUse_false_when_starts_at_zero()
    {
        var thumbInfo = new ThumbInfo { ThumbCounts = 3 };
        thumbInfo.Add(0);
        thumbInfo.Add(10);
        thumbInfo.Add(20);

        Assert.False(FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec: 60));
    }

    [Fact]
    public void CanUse_false_for_uneven_spacing()
    {
        var thumbInfo = new ThumbInfo { ThumbCounts = 3 };
        thumbInfo.Add(100);
        thumbInfo.Add(200);
        thumbInfo.Add(350);

        Assert.False(FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec: 600));
    }

    [Fact]
    public void BuildTileFilter_uses_fps_and_tile()
    {
        string vf = FfmpegOnePassCreator.BuildTileFilter(
            intervalSec: 720,
            width: 220,
            height: 124,
            cols: 3,
            rows: 3,
            durationSec: 7200,
            panelCount: 9,
            scaleFlags: "bilinear");

        Assert.Contains("fps=1/720", vf);
        Assert.Contains("tile=3x3", vf);
        Assert.Contains("scale=220:124", vf);
    }
}
