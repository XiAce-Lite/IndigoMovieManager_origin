using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailDurationResolverTests
{
    [Theory]
    [InlineData(1800, 0, 9000, 1800)]
    [InlineData(0, 1800, 9000, 1800)]
    [InlineData(0, 0, 1800, 1800)]
    [InlineData(7200, 0, 0, 7200)]
    [InlineData(0, 7200, 9000, 7200)]
    public void PickBestDuration_prefers_ffprobe_then_shell_then_opencv(
        double ffprobe,
        double shell,
        double openCv,
        double expected)
    {
        double actual = ThumbnailDurationResolver.PickBestDuration(ffprobe, shell, openCv);
        Assert.Equal(expected, actual);
    }
}

public class ThumbnailSamplingPolicyTests
{
    [Theory]
    [InlineData(1800, false, 1800)]
    [InlineData(3600, false, 3600)]
    [InlineData(6805, false, 6805)]
    [InlineData(7200, false, 7200)]
    [InlineData(7200, true, 7200)]
    [InlineData(0, false, 0)]
    public void GetEffectiveSamplingDuration_uses_full_duration(
        double durationSec,
        bool isManual,
        double expected)
    {
        double actual = ThumbnailSamplingPolicy.GetEffectiveSamplingDuration(durationSec, isManual);
        Assert.Equal(expected, actual);
    }
}
