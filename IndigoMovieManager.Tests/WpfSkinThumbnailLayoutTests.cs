using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinThumbnailLayoutTests
{
    [Fact]
    public void FromWpfSkinThumbnail_null_defaults()
    {
        ThumbnailLayoutSpec spec = ThumbnailLayoutSpec.FromWpfSkinThumbnail(null);
        Assert.Equal(400, spec.Width);
        Assert.Equal(225, spec.Height);
        Assert.Equal(1, spec.Columns);
        Assert.Equal(1, spec.Rows);
    }

    [Fact]
    public void FromWpfSkinThumbnail_clamps_non_positive()
    {
        var thumb = new WpfSkinThumbnail
        {
            Width = 0,
            Height = -5,
            Columns = 0,
            Rows = -1,
        };
        ThumbnailLayoutSpec spec = ThumbnailLayoutSpec.FromWpfSkinThumbnail(thumb);
        Assert.Equal(1, spec.Width);
        Assert.Equal(1, spec.Height);
        Assert.Equal(1, spec.Columns);
        Assert.Equal(1, spec.Rows);
    }

    [Fact]
    public void FromWpfSkinThumbnail_same_dims_share_key()
    {
        var a = new WpfSkinThumbnail { Width = 360, Height = 203, Columns = 1, Rows = 1 };
        var b = new WpfSkinThumbnail { Width = 360, Height = 203, Columns = 1, Rows = 1 };
        Assert.Equal(
            ThumbnailLayoutSpec.FromWpfSkinThumbnail(a).Key,
            ThumbnailLayoutSpec.FromWpfSkinThumbnail(b).Key);
    }

    [Fact]
    public void GetActiveListLayout_null_CurrentThumbnailLayout_uses_default_spec()
    {
        ThumbnailLayoutSpec previous = WpfSkinSettings.CurrentThumbnailLayout;
        try
        {
            WpfSkinSettings.CurrentThumbnailLayout = null;
            ThumbnailLayoutSpec spec = ThumbnailLayoutResolver.GetActiveListLayout(SkinEngine.Wpf);
            Assert.Equal("400x225x1x1", spec.Key);
        }
        finally
        {
            WpfSkinSettings.CurrentThumbnailLayout = previous;
        }
    }

    [Fact]
    public void ResolveThumbPathsForEngine_null_layout_still_assigns_wpf_paths()
    {
        ThumbnailLayoutSpec previous = WpfSkinSettings.CurrentThumbnailLayout;
        try
        {
            WpfSkinSettings.CurrentThumbnailLayout = null;
            var rec = new MovieRecords
            {
                Movie_Name = "sample.mp4",
                Movie_Body = "sample",
                Movie_Path = @"C:\temp\sample.mp4",
            };
            var cache = new ThumbnailLayoutCache();
            cache.Refresh(
                dbName: "testdb",
                thumbFolder: Path.Combine(Path.GetTempPath(), "imm-wpf-skin-thumb-test"));
            ThumbnailLayoutResolver.ResolveThumbPathsForEngine([rec], cache, SkinEngine.Wpf);
            Assert.False(string.IsNullOrEmpty(rec.ThumbPathWpfSkin));
        }
        finally
        {
            WpfSkinSettings.CurrentThumbnailLayout = previous;
        }
    }
}
