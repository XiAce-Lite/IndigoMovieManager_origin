using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class PreGenThumbSkinSelectionTests
{
    [Theory]
    [InlineData("Wpf:CardLarge", true, "CardLarge")]
    [InlineData("Wb:DefaultGrid", false, "DefaultGrid")]
    [InlineData("wpf:abc", true, "abc")]
    public void TryParseKey_accepts_known_prefixes(string key, bool expectWpf, string expectName)
    {
        Assert.True(PreGenThumbSkinSelection.TryParseKey(key, out bool isWpf, out string name));
        Assert.Equal(expectWpf, isWpf);
        Assert.Equal(expectName, name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CardLarge")]
    [InlineData("Other:x")]
    public void TryParseKey_rejects_invalid(string key)
    {
        Assert.False(PreGenThumbSkinSelection.TryParseKey(key, out _, out _));
    }

    [Fact]
    public void FormatKeys_and_stored_round_trip()
    {
        Assert.Equal("Wpf:CardLarge", PreGenThumbSkinSelection.FormatWpfKey("CardLarge"));
        Assert.Equal("Wb:DefaultGrid", PreGenThumbSkinSelection.FormatWbKey("DefaultGrid"));
        Assert.Equal("1", PreGenThumbSkinSelection.FormatEnabled(true));
        Assert.Equal("0", PreGenThumbSkinSelection.FormatEnabled(false));
        Assert.True(PreGenThumbSkinSelection.ParseEnabled("1"));
        Assert.False(PreGenThumbSkinSelection.ParseEnabled("0"));

        string stored = PreGenThumbSkinSelection.FormatStoredKeys(
        [
            PreGenThumbSkinSelection.FormatWbKey("DefaultGrid"),
            PreGenThumbSkinSelection.FormatWpfKey("CardLarge"),
            PreGenThumbSkinSelection.FormatWpfKey("CardLarge"),
        ]);
        HashSet<string> parsed = PreGenThumbSkinSelection.ParseStoredKeys(stored);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(PreGenThumbSkinSelection.FormatWpfKey("CardLarge"), parsed);
        Assert.Contains(PreGenThumbSkinSelection.FormatWbKey("DefaultGrid"), parsed);
    }

    [Fact]
    public void ResolveUniqueLayouts_dedupes_same_dimensions()
    {
        IReadOnlyList<string> wpf = WpfSkinLoader.EnumerateSkins();
        if (wpf.Count == 0)
        {
            return;
        }

        string key = PreGenThumbSkinSelection.FormatWpfKey(wpf[0]);
        IReadOnlyList<ThumbnailLayoutSpec> once =
            PreGenThumbSkinSelection.ResolveUniqueLayouts([key, key]);
        Assert.True(once.Count <= 1);
        if (once.Count == 1)
        {
            Assert.False(string.IsNullOrWhiteSpace(once[0].Key));
        }
    }

    [Fact]
    public void BuildOptionsFromDisk_excludes_missing_saved_keys()
    {
        var phantom = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PreGenThumbSkinSelection.FormatWpfKey("__missing_skin_xyz__"),
            PreGenThumbSkinSelection.FormatWbKey("__missing_wb_xyz__"),
        };

        IReadOnlyList<PreGenThumbSkinSelection.SkinOption> options =
            PreGenThumbSkinSelection.BuildOptionsFromDisk(phantom);

        Assert.DoesNotContain(options, o => o.Key.Contains("__missing_", StringComparison.OrdinalIgnoreCase));
        Assert.All(options, o => Assert.False(o.IsChecked));
    }
}
