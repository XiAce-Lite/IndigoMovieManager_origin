using System.Text.Json;
using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinSpacingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_blank_is_empty(string text)
    {
        WpfSkinSpacing s = WpfSkinSpacing.Parse(text);
        Assert.True(s.IsEmpty);
    }

    [Fact]
    public void Parse_uniform()
    {
        WpfSkinSpacing s = WpfSkinSpacing.Parse("8");
        Assert.Equal(8, s.Left);
        Assert.Equal(8, s.Top);
        Assert.Equal(8, s.Right);
        Assert.Equal(8, s.Bottom);
    }

    [Fact]
    public void Parse_pair()
    {
        WpfSkinSpacing s = WpfSkinSpacing.Parse("4,8");
        Assert.Equal(4, s.Left);
        Assert.Equal(8, s.Top);
        Assert.Equal(4, s.Right);
        Assert.Equal(8, s.Bottom);
    }

    [Fact]
    public void Parse_quad()
    {
        WpfSkinSpacing s = WpfSkinSpacing.Parse("1,2,3,4");
        Assert.Equal(1, s.Left);
        Assert.Equal(2, s.Top);
        Assert.Equal(3, s.Right);
        Assert.Equal(4, s.Bottom);
    }

    [Theory]
    [InlineData("a,b")]
    [InlineData("1,2,3")]
    public void Parse_invalid_is_empty(string text)
    {
        Assert.True(WpfSkinSpacing.Parse(text).IsEmpty);
    }

    [Fact]
    public void JsonConverter_number_uniform()
    {
        var node = DeserializeNode("""{"margin":8}""");
        Assert.NotNull(node.Margin);
        Assert.Equal(8, node.Margin.Left);
        Assert.Equal(8, node.Margin.Bottom);
    }

    [Fact]
    public void JsonConverter_string_pair()
    {
        var node = DeserializeNode("""{"margin":"4,8"}""");
        Assert.NotNull(node.Margin);
        Assert.Equal(4, node.Margin.Left);
        Assert.Equal(8, node.Margin.Top);
    }

    [Fact]
    public void JsonConverter_null()
    {
        var node = DeserializeNode("""{"margin":null}""");
        Assert.Null(node.Margin);
    }

    [Fact]
    public void JsonConverter_array_pair()
    {
        // 実装どおり配列 2 要素は左右/上下として解釈される
        var node = DeserializeNode("""{"margin":[1,2]}""");
        Assert.NotNull(node.Margin);
        Assert.Equal(1, node.Margin.Left);
        Assert.Equal(2, node.Margin.Top);
        Assert.Equal(1, node.Margin.Right);
        Assert.Equal(2, node.Margin.Bottom);
    }

    [Fact]
    public void JsonConverter_object_token_throws()
    {
        Assert.ThrowsAny<JsonException>(() => DeserializeNode("""{"margin":{"left":1}}"""));
    }

    private static WpfSkinNode DeserializeNode(string json) =>
        JsonSerializer.Deserialize<WpfSkinNode>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
}
