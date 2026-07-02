using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbPathHelperTests
{
    private static readonly ThumbnailLayoutSpec SharedListLayout = new(120, 90, 3, 1);

    [Fact]
    public void ApplyThumbPaths_updates_WpfSkin_path_when_WPF_engine_active_even_if_layout_matches_WB()
    {
        string previousWbSkin = WhiteBrowserSkinSettings.ActiveSkinFolder;
        try
        {
            WhiteBrowserSkinSettings.ActiveSkinFolder = "DefaultSmall";
            var record = new MovieRecords { Movie_Id = 1 };
            var queueObj = new QueueObj
            {
                MovieId = 1,
                ThumbnailLayout = SharedListLayout,
            };
            const string thumbPath = @"C:\thumbs\120x90x3x1\movie.#abc.jpg";

            ThumbPathHelper.ApplyThumbPaths([record], queueObj, thumbPath, SkinEngine.Wpf);

            Assert.Equal(thumbPath, record.ThumbPathWpfSkin);
            Assert.Equal("", record.ThumbPathWb);
        }
        finally
        {
            WhiteBrowserSkinSettings.ActiveSkinFolder = previousWbSkin;
        }
    }

    [Fact]
    public void ApplyThumbPaths_updates_Wb_path_when_WB_engine_active()
    {
        var record = new MovieRecords { Movie_Id = 1 };
        var queueObj = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = SharedListLayout,
        };
        const string thumbPath = @"C:\thumbs\120x90x3x1\movie.#abc.jpg";

        ThumbPathHelper.ApplyThumbPaths([record], queueObj, thumbPath, SkinEngine.Wb);

        Assert.Equal(thumbPath, record.ThumbPathWb);
        Assert.Equal("", record.ThumbPathWpfSkin);
    }

    [Fact]
    public void ApplyThumbPaths_updates_detail_path_for_detail_layout()
    {
        var record = new MovieRecords { Movie_Id = 1 };
        var queueObj = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = ThumbnailLayoutSpec.DetailPaneLayout,
        };
        const string thumbPath = @"C:\thumbs\120x90x1x1\movie.#abc.jpg";

        ThumbPathHelper.ApplyThumbPaths([record], queueObj, thumbPath, SkinEngine.Wpf);

        Assert.Equal(thumbPath, record.ThumbDetail);
    }
}
