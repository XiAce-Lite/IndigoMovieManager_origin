using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinFieldCatalogTests
{
    [Fact]
    public void CollectUsedFieldIds_finds_thumbnail_tags_and_text_fields()
    {
        var root = new WpfSkinNode
        {
            Panel = "stack",
            Children =
            [
                new WpfSkinNode { Type = "thumbnail" },
                new WpfSkinNode { Type = "text", Field = "title" },
                new WpfSkinNode { Type = "tags" },
                new WpfSkinNode { Type = "text", Field = "dir" },
            ],
        };

        HashSet<string> used = WpfSkinFieldCatalog.CollectUsedFieldIds(root);

        Assert.Contains("thumbnail", used);
        Assert.Contains("title", used);
        Assert.Contains("tags", used);
        Assert.Contains("dir", used);
        Assert.DoesNotContain("path", used);
    }

    [Fact]
    public void TryInsertField_rejects_duplicate()
    {
        var root = new WpfSkinNode
        {
            Panel = "stack",
            Children = [new WpfSkinNode { Type = "text", Field = "title" }],
        };

        bool ok = WpfSkinLayoutEditor.TryInsertField(root, root, "title", 1, out _, out string error);

        Assert.False(ok);
        Assert.Contains("既に配置", error);
        Assert.Single(root.Children);
    }

    [Fact]
    public void TryInsertField_creates_path_node_with_link()
    {
        var root = new WpfSkinNode { Panel = "stack", Children = [] };

        bool ok = WpfSkinLayoutEditor.TryInsertField(root, root, "dir", 0, out WpfSkinNode added, out string error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("text", added.Type);
        Assert.Equal("dir", added.Field);
        Assert.True(added.Link);
        Assert.Equal("親フォルダ:", added.Label);
    }

    [Fact]
    public void CreateNodeFromField_sets_title_style_for_title_like_fields()
    {
        WpfSkinNode title = WpfSkinLayoutEditor.CreateNodeFromField("title");
        WpfSkinNode metatitle = WpfSkinLayoutEditor.CreateNodeFromField("metatitle");

        Assert.Equal("title", title.Style);
        Assert.Equal("title", metatitle.Style);
    }

    [Fact]
    public void CreateNodeFromField_sets_meta_style_for_metadata_fields()
    {
        WpfSkinNode node = WpfSkinLayoutEditor.CreateNodeFromField("artist");

        Assert.Equal("meta", node.Style);
        Assert.True(node.Wrap);
    }

    [Fact]
    public void CreateNodeFromField_sets_path_style_for_path_fields()
    {
        WpfSkinNode node = WpfSkinLayoutEditor.CreateNodeFromField("comment1");

        Assert.Equal("path", node.Style);
        Assert.True(node.Link);
    }

    [Fact]
    public void CreateNodeFromField_sets_list_header_for_list_skin()
    {
        WpfSkinNode node = WpfSkinLayoutEditor.CreateNodeFromField("viewcount", isListSkin: true);

        Assert.Equal("視聴回数", node.Header);
        Assert.True(string.IsNullOrEmpty(node.Label));
    }

    [Fact]
    public void TryInsertField_rejects_second_insert_of_same_field()
    {
        var root = new WpfSkinNode { Panel = "stack", Children = [] };
        Assert.True(WpfSkinLayoutEditor.TryInsertField(root, root, "viewcount", 0, out _, out _));
        Assert.False(WpfSkinLayoutEditor.TryInsertField(root, root, "viewcount", 1, out _, out string error));
        Assert.Contains("既に配置", error);
        Assert.Single(root.Children);
    }

    [Fact]
    public void UnusedFields_hides_placed_items()
    {
        var root = new WpfSkinNode
        {
            Panel = "stack",
            Children = [new WpfSkinNode { Type = "thumbnail" }],
        };

        List<string> unused = WpfSkinFieldCatalog.UnusedFields(root).Select(f => f.Id).ToList();

        Assert.DoesNotContain("thumbnail", unused);
        Assert.Contains("title", unused);
        Assert.Contains("tags", unused);
    }

    [Fact]
    public void Comment1_is_path_kind_for_url_links()
    {
        Assert.True(WpfSkinFieldCatalog.TryGet("comment1", out WpfSkinFieldDescriptor desc));
        Assert.Equal(WpfSkinFieldKind.Path, desc.Kind);
        Assert.True(WpfSkinFieldCatalog.IsPathField("comment1"));
    }
}
