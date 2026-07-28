using System.Windows;
using IndigoMovieManager.Services.WpfSkin;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WpfSkinGridLengthParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_blank_is_Auto(string value)
    {
        GridLength gl = WpfSkinGridLengthParser.Parse(value);
        Assert.True(gl.IsAuto);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("AUTO")]
    public void Parse_auto_is_Auto(string value)
    {
        Assert.True(WpfSkinGridLengthParser.Parse(value).IsAuto);
    }

    [Theory]
    [InlineData("*", 1.0)]
    [InlineData("2*", 2.0)]
    [InlineData("0.5*", 0.5)]
    [InlineData("  *  ", 1.0)]
    public void Parse_star(string value, double expected)
    {
        GridLength gl = WpfSkinGridLengthParser.Parse(value);
        Assert.True(gl.IsStar);
        Assert.Equal(expected, gl.Value);
    }

    [Theory]
    [InlineData("*foo")]
    [InlineData("abc*")]
    [InlineData("**")]
    public void Parse_invalid_star_is_Auto(string value)
    {
        Assert.True(WpfSkinGridLengthParser.Parse(value).IsAuto);
    }

    [Theory]
    [InlineData("120", 120.0)]
    [InlineData("12.5", 12.5)]
    [InlineData("-10", -10.0)]
    public void Parse_pixel(string value, double expected)
    {
        GridLength gl = WpfSkinGridLengthParser.Parse(value);
        Assert.True(gl.IsAbsolute);
        Assert.Equal(expected, gl.Value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("infinity")]
    public void Parse_NaN_or_Infinity_throws(string value)
    {
        // 実装どおり: double.TryParse は成功し、GridLength コンストラクタが例外を投げる
        Assert.Throws<ArgumentException>(() => WpfSkinGridLengthParser.Parse(value));
    }
}
