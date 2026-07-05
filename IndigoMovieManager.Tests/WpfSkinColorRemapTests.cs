using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinColorRemapTests
{
    [Theory]
    [InlineData("#FFFFFF", "#1E1E1E")]
    [InlineData("#000000", "#E0E0E0")]
    [InlineData("#555555", "#AAAAAA")]
    [InlineData("#888", "#999999")]
    [InlineData("#F0F0F0", "#2D2D2D")]
    public void RemapIfKnown_maps_default_palette(string input, string expected)
    {
        Assert.Equal(expected, WpfSkinColorRemap.RemapIfKnown(input));
    }

    [Fact]
    public void RemapIfKnown_leaves_unknown_colors()
    {
        Assert.Equal("#ABCDEF", WpfSkinColorRemap.RemapIfKnown("#ABCDEF"));
    }
}

public class WpfSkinColorResolverTests
{
    private static WpfSkinDefinition AdaptiveSkin() => new()
    {
        Name = "DefaultSmall",
        Surface = new WpfSkinSurface { Background = "#FFFFFF" },
    };

    private static WpfSkinDefinition FixedDarkSkin() => new()
    {
        Name = "DarkModeSample",
        ColorProfile = "dark",
        Surface = new WpfSkinSurface { Background = "#1E1E1E" },
    };

    [Fact]
    public void ResolveColor_remaps_when_adaptive_and_app_dark()
    {
        try
        {
            AppThemeService.SetModeFromSettingsString("Dark");
            Assert.Equal("#1E1E1E", WpfSkinColorResolver.ResolveColor("#FFFFFF", AdaptiveSkin()));
        }
        finally
        {
            AppThemeService.SetModeFromSettingsString("Light");
        }
    }

    [Fact]
    public void ResolveColor_keeps_json_when_colorProfile_set()
    {
        try
        {
            AppThemeService.SetModeFromSettingsString("Dark");
            Assert.Equal("#1E1E1E", WpfSkinColorResolver.ResolveColor("#1E1E1E", FixedDarkSkin()));
            Assert.Equal("#FFFFFF", WpfSkinColorResolver.ResolveColor("#FFFFFF", FixedDarkSkin()));
        }
        finally
        {
            AppThemeService.SetModeFromSettingsString("Light");
        }
    }

    [Fact]
    public void ResolveColor_keeps_light_json_when_app_light()
    {
        AppThemeService.SetModeFromSettingsString("Light");
        Assert.Equal("#FFFFFF", WpfSkinColorResolver.ResolveColor("#FFFFFF", AdaptiveSkin()));
    }
}

public class AppThemeServiceTests
{
    [Theory]
    [InlineData("Light", false)]
    [InlineData("Dark", true)]
    [InlineData("dark", true)]
    [InlineData("", false)]
    public void ParseMode_recognizes_values(string input, bool expectDark)
    {
        AppThemeMode mode = AppThemeService.ParseMode(input);
        Assert.Equal(expectDark, AppThemeService.ResolveEffectiveIsDark(mode));
    }
}
