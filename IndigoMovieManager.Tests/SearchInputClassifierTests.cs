using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class SearchInputClassifierTests
{
    [Theory]
    [InlineData("keyword")]
    [InlineData("foo bar")]
    [InlineData("a|b")]
    [InlineData("!tag")]
    [InlineData("-exclude")]
    [InlineData("\"quoted\"")]
    [InlineData("  leading space")]
    public void IsIncrementalSearchEligible_returns_true_for_normal_keywords(string text)
    {
        Assert.True(SearchInputClassifier.IsIncrementalSearchEligible(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void IsIncrementalSearchEligible_returns_false_for_empty_or_whitespace(string text)
    {
        Assert.False(SearchInputClassifier.IsIncrementalSearchEligible(text));
    }

    [Theory]
    [InlineData("{tag = ''}")]
    [InlineData("{::error}")]
    [InlineData("  {::duplication}")]
    public void IsIncrementalSearchEligible_returns_false_for_brace_search(string text)
    {
        Assert.False(SearchInputClassifier.IsIncrementalSearchEligible(text));
    }
}
