using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

[CollectionDefinition("StaWpfSkin", DisableParallelization = true)]
public class StaWpfSkinCollection
{
}

[Collection("StaWpfSkin")]
public class WpfSkinLayoutBuilderTests
{
    [StaFact]
    public void Build_null_returns_null()
    {
        Assert.Null(WpfSkinLayoutBuilder.Build(null, new WpfSkinDefinition()));
    }

    [StaFact]
    public void Build_unknown_type_returns_null()
    {
        var node = new WpfSkinNode { Type = "foo" };
        Assert.Null(WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition()));
    }

    [StaFact]
    public void Build_label_only_sets_static_text()
    {
        var node = new WpfSkinNode { Type = "text", Label = "静的ラベル" };
        var element = WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition()) as TextBlock;
        Assert.NotNull(element);
        Assert.Equal("静的ラベル", element.Text);
        Assert.Null(BindingOperations.GetBinding(element, TextBlock.TextProperty));
    }

    [StaFact]
    public void Build_custom_field_binds_path()
    {
        var node = new WpfSkinNode { Type = "text", Field = "custom" };
        var element = WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition()) as TextBlock;
        Assert.NotNull(element);
        Binding binding = BindingOperations.GetBinding(element, TextBlock.TextProperty);
        Assert.NotNull(binding);
        Assert.Equal("custom", binding.Path.Path);
    }

    [StaFact]
    public void Build_grid_without_row_col_defs_adds_auto()
    {
        var node = new WpfSkinNode
        {
            Panel = "grid",
            Children =
            [
                new WpfSkinNode { Type = "text", Label = "a" },
            ],
        };
        var grid = WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition()) as Grid;
        Assert.NotNull(grid);
        Assert.Single(grid.RowDefinitions);
        Assert.Single(grid.ColumnDefinitions);
    }

    [StaFact]
    public void Build_out_of_range_row_col_does_not_throw()
    {
        var node = new WpfSkinNode
        {
            Panel = "grid",
            Rows = ["auto"],
            Columns = ["*"],
            Children =
            [
                new WpfSkinNode { Type = "text", Label = "x", Row = 5, Col = 9 },
            ],
        };
        UIElement element = WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition());
        Assert.NotNull(element);
    }

    [StaFact]
    public void Build_invalid_foreground_falls_back()
    {
        var node = new WpfSkinNode
        {
            Type = "text",
            Label = "x",
            Foreground = "not-a-color",
        };
        var text = WpfSkinLayoutBuilder.Build(node, new WpfSkinDefinition()) as TextBlock;
        Assert.NotNull(text);
        Assert.NotNull(text.Foreground);
    }

    [StaFact]
    public void Build_thumbnail_stretch_without_height_has_NaN_height()
    {
        var def = new WpfSkinDefinition
        {
            Thumbnail = new WpfSkinThumbnail { Width = 400, Height = 225 },
        };
        var node = new WpfSkinNode
        {
            Type = "thumbnail",
            VAlign = "stretch",
        };
        UIElement element = WpfSkinLayoutBuilder.Build(node, def);
        Assert.NotNull(element);
        var label = Assert.IsType<Label>(element);
        Assert.True(double.IsNaN(label.Height));
    }

    [StaFact]
    public void BuildListHeader_non_list_returns_null()
    {
        var def = new WpfSkinDefinition
        {
            Type = "card",
            Card = new WpfSkinCard
            {
                Layout = new WpfSkinNode
                {
                    Panel = "grid",
                    Children = [new WpfSkinNode { Type = "text", Header = "H", Field = "title" }],
                },
            },
        };
        Assert.Null(WpfSkinLayoutBuilder.BuildListHeader(def));
    }

    [StaFact]
    public void BuildListHeader_list_without_header_returns_null()
    {
        var def = new WpfSkinDefinition
        {
            Type = "list",
            Card = new WpfSkinCard
            {
                Layout = new WpfSkinNode
                {
                    Panel = "grid",
                    Children = [new WpfSkinNode { Type = "text", Field = "title" }],
                },
            },
        };
        Assert.Null(WpfSkinLayoutBuilder.BuildListHeader(def));
    }

    [StaFact]
    public void Build_with_null_host_converters_does_not_throw()
    {
        WpfSkinTemplateBuilder.ApplyHostContext(new WpfSkinTemplateBuilder.BuildContext());
        try
        {
            var def = new WpfSkinDefinition
            {
                Thumbnail = new WpfSkinThumbnail { Width = 100, Height = 50 },
                Card = new WpfSkinCard
                {
                    Layout = new WpfSkinNode
                    {
                        Stack = "vertical",
                        Children =
                        [
                            new WpfSkinNode { Type = "thumbnail" },
                            new WpfSkinNode { Type = "text", Field = "size", Format = "filesize" },
                        ],
                    },
                },
            };
            UIElement element = WpfSkinLayoutBuilder.Build(def.Card.Layout, def);
            Assert.NotNull(element);
        }
        finally
        {
            WpfSkinTemplateBuilder.ApplyHostContext(new WpfSkinTemplateBuilder.BuildContext());
        }
    }
}
