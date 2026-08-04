using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinStructTemplateTests
{
    [Fact]
    public void Catalog_includes_jacket_local_side()
    {
        WpfSkinStructTemplate tmpl = WpfSkinStructTemplateCatalog.All
            .Single(t => t.Id == "jacket_local_side");

        Assert.True(tmpl.UseCoexistSources);
        Assert.Equal(120, tmpl.ThumbWidth);
        Assert.Equal(90, tmpl.ThumbHeight);
        Assert.Equal(5, tmpl.ThumbColumns);
        Assert.Equal(2, tmpl.ThumbRows);
    }

    [Fact]
    public void CreateFromStructTemplate_jacket_local_side_sets_sources_and_split_nodes()
    {
        WpfSkinStructTemplate tmpl = WpfSkinStructTemplateCatalog.All
            .Single(t => t.Id == "jacket_local_side");

        WpfSkinDefinition def = WpfSkinStorage.CreateFromStructTemplate(tmpl);

        Assert.False(def.Thumbnail.PreferJacket);
        Assert.Equal(["comment1", "local"], WpfSkinThumbnailSources.Normalize(def.Thumbnail.Sources));
        Assert.Equal(120, def.Thumbnail.Width);
        Assert.Equal(90, def.Thumbnail.Height);
        Assert.Equal(5, def.Thumbnail.Columns);
        Assert.Equal(2, def.Thumbnail.Rows);
        Assert.Equal(980, def.Card.Width);

        HashSet<string> used = WpfSkinFieldCatalog.CollectUsedFieldIds(def.Card.Layout);
        Assert.Contains(WpfSkinFieldCatalog.ThumbnailJacketId, used);
        Assert.Contains(WpfSkinFieldCatalog.ThumbnailLocalId, used);
        Assert.Contains("title", used);
        Assert.Contains("tags", used);
    }
}
