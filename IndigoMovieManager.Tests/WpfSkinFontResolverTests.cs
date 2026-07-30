using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinFontResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveFamilyName_blank_returns_default(string requested)
    {
        Assert.Equal(WpfSkinFontResolver.DefaultFontFamily, WpfSkinFontResolver.ResolveFamilyName(requested));
    }

    [Fact]
    public void ResolveFamilyName_missing_font_returns_default()
    {
        string resolved = WpfSkinFontResolver.ResolveFamilyName("__NoSuchFont_IndigoMovieManager__");
        Assert.Equal(WpfSkinFontResolver.DefaultFontFamily, resolved);
    }

    [Fact]
    public void ResolveFamilyName_keeps_installed_font()
    {
        // 環境依存だが Windows 標準の Arial を想定
        if (!WpfSkinFontResolver.IsInstalled("Arial"))
        {
            return;
        }

        Assert.Equal("Arial", WpfSkinFontResolver.ResolveFamilyName("Arial"));
    }
}
