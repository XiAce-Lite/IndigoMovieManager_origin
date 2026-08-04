namespace IndigoMovieManager.Tests;

using IndigoMovieManager.Services;
using Xunit;

public class BraceFieldSearchBuilderTests
{
    [Fact]
    public void BuildArtistEquals_EscapesSingleQuotes()
    {
        string actual = BraceFieldSearchBuilder.BuildArtistEquals("メーカー's");
        Assert.Equal("{artist = 'メーカー''s'}", actual);
    }

    [Fact]
    public void BuildComment3Like_EscapesQuoteAndLikeMeta()
    {
        string actual = BraceFieldSearchBuilder.BuildComment3Like("a%b_c'd");
        Assert.Equal(@"{comment3 like '%a\%b\_c''d%' ESCAPE '\'}", actual);
    }

    [Fact]
    public void BuildArtistEquals_Empty_ReturnsEmpty()
    {
        Assert.Equal("", BraceFieldSearchBuilder.BuildArtistEquals("  "));
    }
}
