using System.Text.Json;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class JacketInfoSkinLoadTests
{
    [Fact]
    public void JacketInfo_deserializes_preferJacket_grid_not_CardLarge()
    {
        string path = ResolveSampleSkinPath("JacketInfo");
        Assert.True(File.Exists(path), path);

        string json = File.ReadAllText(path);
        var opt = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        WpfSkinDefinition def = JsonSerializer.Deserialize<WpfSkinDefinition>(json, opt);
        Assert.NotNull(def);
        Assert.Equal("JacketInfo", def.Name);
        Assert.True(def.Thumbnail.PreferJacket);
        Assert.Equal("360x203x1x1", ThumbnailLayoutSpec.FromWpfSkinThumbnail(def.Thumbnail).Key);
        Assert.True(def.Card.Layout.IsGrid);
        Assert.Equal(645, def.Card.Width);
    }

    [Fact]
    public void JacketInfo3x2_deserializes_preferJacket_3x2()
    {
        string path = ResolveSampleSkinPath("JacketInfo3x2");
        Assert.True(File.Exists(path), path);

        Assert.True(WpfSkinLoader.TryLoad("JacketInfo3x2", out WpfSkinDefinition def));
        Assert.Equal("JacketInfo3x2", def.Name);
        Assert.True(def.Thumbnail.PreferJacket);
        Assert.Equal(3, def.Thumbnail.Columns);
        Assert.Equal(2, def.Thumbnail.Rows);
        Assert.Equal("360x203x3x2", ThumbnailLayoutSpec.FromWpfSkinThumbnail(def.Thumbnail).Key);
    }

    [Fact]
    public void SpacingConverter_reads_numeric_array()
    {
        const string json = """{"margin":[0,6,0,0]}""";
        var node = JsonSerializer.Deserialize<WpfSkinNode>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        Assert.NotNull(node);
        Assert.NotNull(node.Margin);
        Assert.Equal(0, node.Margin.Left);
        Assert.Equal(6, node.Margin.Top);
        Assert.Equal(0, node.Margin.Right);
        Assert.Equal(0, node.Margin.Bottom);
    }

    private static string ResolveSampleSkinPath(string folderName)
    {
        string fromBase = Path.Combine(AppContext.BaseDirectory, "Skins", "Wpf", folderName, "skin.json");
        if (File.Exists(fromBase))
        {
            return fromBase;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Skins", "Wpf", folderName, "skin.json"));
    }
}
