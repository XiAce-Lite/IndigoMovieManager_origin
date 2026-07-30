using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests
{
    public class WpfSkinAspectMathTests
    {
        [Theory]
        [InlineData(360, 16, 9, 203)] // 202.5 → 切り上げ 203（Round だと 202）
        [InlineData(400, 16, 9, 225)]
        [InlineData(160, 4, 3, 120)]
        public void HeightFromWidth_uses_ceiling(int width, int rw, int rh, int expected)
        {
            Assert.Equal(expected, WpfSkinAspectMath.HeightFromWidth(width, rw, rh));
        }
    }

    public class ThumbnailLayoutNearMatchTests
    {
        [Fact]
        public void IsNear_detects_1px_height_difference()
        {
            var a = new ThumbnailLayoutSpec(360, 203, 1, 1);
            var b = new ThumbnailLayoutSpec(360, 202, 1, 1);
            Assert.True(ThumbnailLayoutNearMatch.IsNear(a, b));
            Assert.False(ThumbnailLayoutNearMatch.IsNear(a, a));
            Assert.False(ThumbnailLayoutNearMatch.IsNear(a, new ThumbnailLayoutSpec(360, 200, 1, 1)));
        }

        [Fact]
        public void TryParseKey_parses_layout_folder_name()
        {
            Assert.True(ThumbnailLayoutNearMatch.TryParseKey("360x203x1x1", out ThumbnailLayoutSpec spec));
            Assert.Equal(360, spec.Width);
            Assert.Equal(203, spec.Height);
            Assert.Equal(1, spec.Columns);
            Assert.Equal(1, spec.Rows);
        }
    }
}
