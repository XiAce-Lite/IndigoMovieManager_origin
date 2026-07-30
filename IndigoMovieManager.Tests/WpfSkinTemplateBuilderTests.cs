using System.Windows.Controls;
using IndigoMovieManager.Services.WpfSkin;
using WpfToolkit.Controls;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinTemplateBuilderTests
{
    [Fact]
    public void BuildItemsPanel_null_def_is_wrap_panel()
    {
        ItemsPanelTemplate panel = WpfSkinTemplateBuilder.BuildItemsPanel(null);
        Assert.NotNull(panel);
        Assert.Equal(typeof(VirtualizingWrapPanel), panel.VisualTree.Type);
    }

    [Fact]
    public void BuildItemsPanel_list_is_stack_panel()
    {
        var def = new WpfSkinDefinition { Type = "list" };
        ItemsPanelTemplate panel = WpfSkinTemplateBuilder.BuildItemsPanel(def);
        Assert.Equal(typeof(VirtualizingStackPanel), panel.VisualTree.Type);
    }

    [Fact]
    public void BuildItemsPanel_card_stretch_uses_wrap_panel()
    {
        var def = new WpfSkinDefinition
        {
            Type = "card",
            Card = new WpfSkinCard { Stretch = true, Width = 600, Height = 400 },
        };
        ItemsPanelTemplate panel = WpfSkinTemplateBuilder.BuildItemsPanel(def);
        Assert.Equal(typeof(VirtualizingWrapPanel), panel.VisualTree.Type);
    }

    [Fact]
    public void ParseSurfaceBackground_invalid_color_returns_null()
    {
        var def = new WpfSkinDefinition
        {
            Surface = new WpfSkinSurface { Background = "not-a-color" },
        };
        Assert.Null(WpfSkinTemplateBuilder.ParseSurfaceBackground(def));
    }

    [Fact]
    public void ParseSurfaceBackground_blank_returns_null()
    {
        var def = new WpfSkinDefinition
        {
            Surface = new WpfSkinSurface { Background = "" },
        };
        Assert.Null(WpfSkinTemplateBuilder.ParseSurfaceBackground(def));
    }
}
