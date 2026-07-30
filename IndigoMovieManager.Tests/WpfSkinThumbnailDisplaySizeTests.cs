using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests
{
    public class WpfSkinThumbnailDisplaySizeTests
    {
        [Fact]
        public void CalcDisplayHeight_1x1_matches_aspect()
        {
            var thumb = new WpfSkinThumbnail { Width = 400, Height = 225, Columns = 1, Rows = 1 };
            double h = WpfSkinThumbnailDisplaySize.CalcDisplayHeight(200, thumb);
            Assert.Equal(112.5, h, 3);
        }

        [Fact]
        public void CalcDisplayHeight_grid_uses_cell_aspect_times_rows()
        {
            // 参照 360×203 / 3×2 → セル 120×101.5、表示幅 300 → セル幅 100 → セル高 ≈84.583 → 枠高 ≈169.167
            var thumb = new WpfSkinThumbnail { Width = 360, Height = 203, Columns = 3, Rows = 2 };
            double h = WpfSkinThumbnailDisplaySize.CalcDisplayHeight(300, thumb);
            double expected = 300.0 * 203.0 / 360.0;
            Assert.Equal(expected, h, 3);
        }

        [Fact]
        public void CalcDisplayHeight_zero_width_returns_zero()
        {
            var thumb = new WpfSkinThumbnail { Width = 400, Height = 225 };
            Assert.Equal(0, WpfSkinThumbnailDisplaySize.CalcDisplayHeight(0, thumb));
        }

        [Fact]
        public void ShouldTrackParentWidth_thumbnail_always_tracks()
        {
            Assert.True(WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(new WpfSkinNode { Type = "thumbnail" }));
            Assert.True(WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(new WpfSkinNode { Type = "thumbnail", Width = 160 }));
            Assert.False(WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(new WpfSkinNode { Type = "text", Width = 160 }));
            Assert.True(WpfSkinThumbnailDisplaySize.ShouldTrackParentWidth(new WpfSkinNode { Type = "text" }));
        }

        [Fact]
        public void ShouldAutoHeight_when_node_height_absent()
        {
            Assert.True(WpfSkinThumbnailDisplaySize.ShouldAutoHeight(new WpfSkinNode { Type = "thumbnail" }));
            Assert.False(WpfSkinThumbnailDisplaySize.ShouldAutoHeight(new WpfSkinNode { Type = "thumbnail", Height = 120 }));
        }
    }
}
