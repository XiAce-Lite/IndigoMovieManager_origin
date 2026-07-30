using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Services.WpfSkin.Design;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinLayoutEditorTests
{
    [Fact]
    public void AddChild_adds_new_leaf_to_container()
    {
        var root = new WpfSkinNode
        {
            Panel = "stack",
            Children = [],
        };

        WpfSkinNode child = WpfSkinLayoutEditor.AddChild(root, WpfSkinNodeKind.Text);

        Assert.Single(root.Children);
        Assert.Same(child, root.Children[0]);
        Assert.Equal("text", child.Type);
    }

    [Fact]
    public void MoveNode_reorders_siblings()
    {
        var first = new WpfSkinNode { Type = "text", Label = "A" };
        var second = new WpfSkinNode { Type = "text", Label = "B" };
        var siblings = new List<WpfSkinNode> { first, second };

        bool moved = WpfSkinLayoutEditor.MoveNode(siblings, second, -1);

        Assert.True(moved);
        Assert.Same(second, siblings[0]);
        Assert.Same(first, siblings[1]);
    }

    [Fact]
    public void InsertChild_inserts_at_requested_index()
    {
        var root = new WpfSkinNode
        {
            Panel = "stack",
            Children =
            [
                new WpfSkinNode { Type = "text", Label = "A" },
                new WpfSkinNode { Type = "text", Label = "B" },
            ],
        };

        WpfSkinNode child = WpfSkinLayoutEditor.InsertChild(root, WpfSkinNodeKind.Tags, 1);

        Assert.Equal(3, root.Children.Count);
        Assert.Same(child, root.Children[1]);
        Assert.Equal("tags", child.Type);
    }

    [Fact]
    public void MoveNodeToParent_moves_child_between_containers()
    {
        var moved = new WpfSkinNode { Type = "text", Label = "MoveMe" };
        var source = new WpfSkinNode
        {
            Panel = "stack",
            Children = [moved],
        };
        var target = new WpfSkinNode
        {
            Panel = "stack",
            Children =
            [
                new WpfSkinNode { Type = "text", Label = "Existing" },
            ],
        };

        bool ok = WpfSkinLayoutEditor.MoveNodeToParent(source, moved, target, 0);

        Assert.True(ok);
        Assert.Empty(source.Children);
        Assert.Same(moved, target.Children[0]);
    }

    [Fact]
    public void RenameStyle_updates_layout_references()
    {
        var definition = new WpfSkinDefinition
        {
            Styles = new Dictionary<string, WpfSkinStyle>
            {
                ["title"] = new(),
            },
            Card = new WpfSkinCard
            {
                Layout = new WpfSkinNode
                {
                    Panel = "stack",
                    Children =
                    [
                        new WpfSkinNode { Type = "text", Style = "title" },
                    ],
                },
            },
        };

        bool renamed = WpfSkinLayoutEditor.TryRenameStyle(definition, "title", "headline", out string error);

        Assert.True(renamed);
        Assert.Null(error);
        Assert.Contains("headline", definition.Styles.Keys);
        Assert.Equal("headline", definition.Card.Layout.Children[0].Style);
    }

    [Fact]
    public void DeleteStyle_clears_layout_references()
    {
        var definition = new WpfSkinDefinition
        {
            Styles = new Dictionary<string, WpfSkinStyle>
            {
                ["title"] = new(),
            },
            Card = new WpfSkinCard
            {
                Layout = new WpfSkinNode
                {
                    Panel = "stack",
                    Children =
                    [
                        new WpfSkinNode { Type = "text", Style = "title" },
                    ],
                },
            },
        };

        bool deleted = WpfSkinLayoutEditor.DeleteStyle(definition, "title");

        Assert.True(deleted);
        Assert.Empty(definition.Styles);
        Assert.Equal(string.Empty, definition.Card.Layout.Children[0].Style);
    }

    [Fact]
    public void AssignGridSlot_sets_row_and_col()
    {
        var node = new WpfSkinNode { Type = "text" };

        WpfSkinLayoutEditor.AssignGridSlot(node, 2, 1);

        Assert.Equal(2, node.Row);
        Assert.Equal(1, node.Col);
        Assert.Equal(1, node.RowSpan);
        Assert.Equal(1, node.ColSpan);
    }

    [Fact]
    public void CanContainChildren_requires_explicit_panel_or_existing_children()
    {
        var leaf = new WpfSkinNode { Type = "text" };
        var emptyStack = new WpfSkinNode { Panel = "stack", Children = [] };
        var legacy = new WpfSkinNode
        {
            Children = [new WpfSkinNode { Type = "text" }],
        };

        Assert.False(WpfSkinLayoutEditor.CanContainChildren(leaf));
        Assert.True(WpfSkinLayoutEditor.CanContainChildren(emptyStack));
        Assert.True(WpfSkinLayoutEditor.CanContainChildren(legacy));
    }
}
