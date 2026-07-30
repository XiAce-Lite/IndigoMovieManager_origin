using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests
{
    public class PreviewPlaybackTimingTests
    {
        [Theory]
        [InlineData(24d, 24d)]
        [InlineData(30d, 30d)]
        [InlineData(60d, 60d)]
        [InlineData(120d, 60d)]
        [InlineData(0d, 30d)]
        [InlineData(-1d, 30d)]
        public void NormalizeFps_clamps_to_expected_range(double input, double expected)
        {
            Assert.Equal(expected, PreviewPlaybackTiming.NormalizeFps(input));
        }

        [Fact]
        public void GetTimerInterval_matches_source_fps()
        {
            TimeSpan interval = PreviewPlaybackTiming.GetTimerInterval(30d);
            Assert.Equal(33.333, interval.TotalMilliseconds, 1);
        }

        [Theory]
        [InlineData(500, -100, 0, 1000, 400)]
        [InlineData(50, -100, 0, 1000, 0)]
        [InlineData(950, 100, 0, 1000, 1000)]
        public void ClampSeekMs_clamps_stepped_values(
            int value,
            int delta,
            int minimum,
            int maximum,
            int expected)
        {
            Assert.Equal(expected, PreviewPlaybackTiming.ClampSeekMs(value, delta, minimum, maximum));
        }
    }
}
