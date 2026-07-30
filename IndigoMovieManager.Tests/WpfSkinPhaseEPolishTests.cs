using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinPhaseEPolishTests
{
    [Fact]
    public void CreateFromTemplate_clones_named_skin_as_new()
    {
        WpfSkinDefinition created = WpfSkinStorage.CreateFromTemplate(WpfSkinLoader.DefaultSkinName);

        Assert.Equal("新規スキン", created.Name);
        Assert.Null(created.FolderName);
        Assert.NotNull(created.Card?.Layout);
    }

    [Fact]
    public void CreateFromTemplate_unknown_falls_back_to_default()
    {
        WpfSkinDefinition created = WpfSkinStorage.CreateFromTemplate("__no_such_skin__");

        Assert.Equal("新規スキン", created.Name);
        Assert.NotNull(created.Card?.Layout);
    }

    [Fact]
    public void CreateStylePreset_title_has_bold_14()
    {
        WpfSkinStyle style = WpfSkinLayoutEditor.CreateStylePreset("title");

        Assert.Equal(14, style.FontSize);
        Assert.True(style.Bold);
    }

    [Fact]
    public void TryAddStyle_with_preset_stores_values()
    {
        var def = new WpfSkinDefinition { Styles = new Dictionary<string, WpfSkinStyle>() };
        WpfSkinStyle preset = WpfSkinLayoutEditor.CreateStylePreset("meta");

        Assert.True(WpfSkinLayoutEditor.TryAddStyle(def, "metaLabel", preset, out _));
        Assert.Equal(12, def.Styles["metaLabel"].FontSize);
        Assert.Equal("#666666", def.Styles["metaLabel"].Foreground);
    }

    [Theory]
    [InlineData(10, 10, 50, 100, false, false)]
    [InlineData(10, 60, 50, 100, false, true)]
    [InlineData(10, 50, 100, 50, true, false)]
    [InlineData(60, 50, 100, 50, true, true)]
    public void IsInsertAfter_respects_orientation(
        double x,
        double y,
        double width,
        double height,
        bool horizontal,
        bool expectedAfter)
    {
        bool after = WpfSkinDesignInsertGeometry.IsInsertAfter(new System.Windows.Point(x, y), width, height, horizontal);
        Assert.Equal(expectedAfter, after);
    }

    [Fact]
    public void TemplateCatalog_Available_includes_CardLarge()
    {
        IReadOnlyList<WpfSkinTemplateCatalog.Entry> entries = WpfSkinTemplateCatalog.Available();
        Assert.Contains(entries, e => e.FolderName == "CardLarge");
    }
}
