namespace IndigoMovieManager.Tests;

using IndigoMovieManager.Services.WpfSkin;
using Xunit;

public class WpfSkinThumbnailSourcesTests
{
    [Fact]
    public void Normalize_KeepsOrder_MaxTwo_DropsInvalid()
    {
        var sources = new List<WpfSkinThumbnailSource>
        {
            new() { Kind = "comment1" },
            new() { Kind = "bogus" },
            new() { Kind = "local" },
            new() { Kind = "comment1" },
            new() { Kind = "local" },
        };

        IReadOnlyList<string> kinds = WpfSkinThumbnailSources.Normalize(sources);
        Assert.Equal(["comment1", "local"], kinds);
    }

    [Fact]
    public void TryGetRenderKinds_ListIgnoresButSourcesRemain()
    {
        var def = new WpfSkinDefinition
        {
            Type = "list",
            Thumbnail = new WpfSkinThumbnail
            {
                Sources = WpfSkinThumbnailSources.CreateDefaultCoexist(),
                PreferJacket = true,
            },
        };

        Assert.False(WpfSkinThumbnailSources.TryGetRenderKinds(def, out _));
        Assert.Equal(2, WpfSkinThumbnailSources.Normalize(def.Thumbnail.Sources).Count);
        Assert.False(WpfSkinThumbnailSources.ShouldSuppressPreferJacket(def));
    }

    [Fact]
    public void TryGetRenderKinds_CardWithSources_SuppressesPreferJacket()
    {
        var def = new WpfSkinDefinition
        {
            Type = "card",
            Thumbnail = new WpfSkinThumbnail
            {
                PreferJacket = true,
                Sources = WpfSkinThumbnailSources.CreateDefaultCoexist(),
            },
        };

        Assert.True(WpfSkinThumbnailSources.TryGetRenderKinds(def, out IReadOnlyList<string> kinds));
        Assert.Equal(["comment1", "local"], kinds);
        Assert.True(WpfSkinThumbnailSources.ShouldSuppressPreferJacket(def));
    }

    [Fact]
    public void Normalize_Comment1Only_HasNoLocal()
    {
        var sources = new List<WpfSkinThumbnailSource> { new() { Kind = "comment1" } };
        IReadOnlyList<string> kinds = WpfSkinThumbnailSources.Normalize(sources);
        Assert.Equal(["comment1"], kinds);
        Assert.DoesNotContain(WpfSkinThumbnailSources.KindLocal, kinds);
    }
}
