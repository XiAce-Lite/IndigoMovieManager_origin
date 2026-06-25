using System.Windows;
using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbPanelHitResolverTests
{
    [Fact]
    public void TryMapClickToCompositePixel_maps_center_of_first_panel()
    {
        bool ok = ThumbPanelHitResolver.TryMapClickToCompositePixel(
            new Point(50, 25),
            controlWidth: 200,
            controlHeight: 100,
            compositePixelWidth: 400,
            compositePixelHeight: 200,
            out double pixelX,
            out double pixelY);

        Assert.True(ok);
        Assert.InRange(pixelX, 0, 199);
        Assert.InRange(pixelY, 0, 99);
    }

    [Fact]
    public void TryMapClickToCompositePixel_rejects_letterbox_click()
    {
        bool ok = ThumbPanelHitResolver.TryMapClickToCompositePixel(
            new Point(10, 50),
            controlWidth: 300,
            controlHeight: 100,
            compositePixelWidth: 400,
            compositePixelHeight: 200,
            out _,
            out _);

        Assert.False(ok);
    }
}
