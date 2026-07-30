using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinDesignGridGeometryTests
{
    [Fact]
    public void HitIndex_uses_equal_slots_when_sizes_missing()
    {
        int index = WpfSkinDesignGridGeometry.HitIndex(75, 100, 4, null);

        Assert.Equal(3, index);
    }

    [Fact]
    public void HitIndex_uses_actual_sizes_when_provided()
    {
        double[] sizes = [10, 40, 50];

        Assert.Equal(0, WpfSkinDesignGridGeometry.HitIndex(5, 100, 3, sizes));
        Assert.Equal(1, WpfSkinDesignGridGeometry.HitIndex(30, 100, 3, sizes));
        Assert.Equal(2, WpfSkinDesignGridGeometry.HitIndex(90, 100, 3, sizes));
    }

    [Fact]
    public void TryGetCellRect_returns_expected_bounds()
    {
        bool ok = WpfSkinDesignGridGeometry.TryGetCellRect(
            width: 100,
            height: 60,
            rows: 2,
            cols: 2,
            row: 1,
            col: 0,
            rowSizes: null,
            colSizes: null,
            out var rect);

        Assert.True(ok);
        Assert.Equal(0, rect.X);
        Assert.Equal(30, rect.Y);
        Assert.Equal(50, rect.Width);
        Assert.Equal(30, rect.Height);
    }

    [Fact]
    public void IsGridPanel_requires_explicit_grid_panel()
    {
        Assert.False(WpfSkinDesignGridGeometry.IsGridPanel(new WpfSkinNode { Type = "text" }));
        Assert.True(WpfSkinDesignGridGeometry.IsGridPanel(new WpfSkinNode { Panel = "grid", Children = [] }));
    }
}
