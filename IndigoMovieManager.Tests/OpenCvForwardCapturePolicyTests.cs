using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class OpenCvForwardCapturePolicyTests
{
    [Fact]
    public void CanUseForwardCapture_false_for_manual()
    {
        var ctx = new ThumbnailJobContext { IsManual = true };

        Assert.False(OpenCvForwardCapturePolicy.CanUseForwardCapture(ctx, [10, 20, 30]));
    }

    [Fact]
    public void CanUseForwardCapture_false_for_non_ascending()
    {
        var ctx = new ThumbnailJobContext { IsManual = false };

        Assert.False(OpenCvForwardCapturePolicy.CanUseForwardCapture(ctx, [10, 5, 30]));
    }

    [Fact]
    public void CanUseForwardCapture_true_for_ascending_auto()
    {
        var ctx = new ThumbnailJobContext { IsManual = false };

        Assert.True(OpenCvForwardCapturePolicy.CanUseForwardCapture(ctx, [60, 120, 180]));
    }

    [Theory]
    [InlineData(0, 5000, 30, true)]
    [InlineData(0, 15000, 30, false)]
    public void ShouldForwardGrab_respects_frame_budget(
        double currentMsec,
        double targetMsec,
        double fps,
        bool expected)
    {
        Assert.Equal(
            expected,
            OpenCvForwardCapturePolicy.ShouldForwardGrab(
                currentMsec,
                targetMsec,
                fps,
                maxForwardGrabs: 300));
    }

    [Fact]
    public void EstimateForwardGrabCount_returns_zero_when_already_near_target()
    {
        Assert.Equal(0, OpenCvForwardCapturePolicy.EstimateForwardGrabCount(10000, 10040, 30));
    }
}
