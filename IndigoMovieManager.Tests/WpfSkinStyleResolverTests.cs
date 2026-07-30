using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinStyleResolverTests
{
    [Fact]
    public void ResolveText_null_styles_uses_node_and_defaults()
    {
        var node = new WpfSkinNode { Type = "text", Field = "title", FontSize = 18, Bold = true };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, null);
        Assert.Equal(18, style.FontSize);
        Assert.True(style.Bold);
        Assert.Equal(WpfSkinFontResolver.DefaultFontFamily, style.FontFamily);
    }

    [Fact]
    public void ResolveText_style_key_is_case_sensitive()
    {
        var styles = new Dictionary<string, WpfSkinStyle>(StringComparer.Ordinal)
        {
            ["title"] = new WpfSkinStyle { FontSize = 20, Bold = true },
        };
        var node = new WpfSkinNode { Type = "text", Style = "Title" };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, styles);
        Assert.Equal(12, style.FontSize);
        Assert.False(style.Bold);
    }

    [Fact]
    public void ResolveText_fontSize_zero_keeps_default()
    {
        var styles = new Dictionary<string, WpfSkinStyle>
        {
            ["t"] = new WpfSkinStyle { FontSize = 0 },
        };
        var node = new WpfSkinNode { Type = "text", Style = "t", FontSize = 0 };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, styles);
        Assert.Equal(12, style.FontSize);
    }

    [Fact]
    public void ResolveText_merges_bold_and_italic()
    {
        var styles = new Dictionary<string, WpfSkinStyle>
        {
            ["t"] = new WpfSkinStyle { Italic = true },
        };
        var node = new WpfSkinNode { Type = "text", Style = "t", Bold = true };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, styles);
        Assert.True(style.Bold);
        Assert.True(style.Italic);
    }

    [Fact]
    public void ResolveText_node_overrides_named_foreground()
    {
        var styles = new Dictionary<string, WpfSkinStyle>
        {
            ["t"] = new WpfSkinStyle { Foreground = "#111111" },
        };
        var node = new WpfSkinNode { Type = "text", Style = "t", Foreground = "#ABCDEF" };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, styles);
        Assert.Equal("#ABCDEF", style.Foreground);
    }

    [Fact]
    public void ResolveText_missing_fontFamily_falls_back()
    {
        var node = new WpfSkinNode
        {
            Type = "text",
            FontFamily = "__NoSuchFont_IndigoMovieManager__",
        };
        ResolvedTextStyle style = WpfSkinStyleResolver.ResolveText(node, null);
        Assert.Equal(WpfSkinFontResolver.DefaultFontFamily, style.FontFamily);
    }
}
