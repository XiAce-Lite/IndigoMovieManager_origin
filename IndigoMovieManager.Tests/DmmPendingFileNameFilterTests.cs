using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmPendingFileNameFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("''")]
    public void Empty_or_blank_quoted_query_matches_all(string query)
    {
        Assert.True(DmmPendingFileNameFilter.IsBroadQuery(query));
        Assert.True(DmmPendingFileNameFilter.Matches("abcd-123.mp4", query));
        Assert.True(DmmPendingFileNameFilter.Matches("日本語タイトルのみのサンプル.mp4", query));
    }

    [Theory]
    [InlineData("abcd-123.mp4", "\"ABCD-123\"", true)]
    [InlineData("abcd-123.mp4", "'abcd-12'", true)]
    [InlineData("abcd-1.mp4", "\"abcd-001\"", false)]
    [InlineData("1abcd00123.mp4", "\"abcd-123\"", false)]
    [InlineData("日本語タイトルのみのサンプル.mp4", "\"日本語タイトル\"", true)]
    [InlineData("日本語タイトルのみのサンプル.mp4", "\"別タイトル\"", false)]
    public void Quoted_query_is_literal_substring(string fileName, string query, bool expected)
    {
        Assert.Equal(expected, DmmPendingFileNameFilter.Matches(fileName, query));
    }

    [Theory]
    [InlineData("1abcd00123.mp4", "abcd-123")]
    [InlineData("ABCD_0123.mp4", "abcd-123")]
    [InlineData("abcd-123a.mp4", "abcd-123")]
    [InlineData("abcd-1.mp4", "abcd-001")]
    [InlineData("abcd 123.mp4", "abcd-123")]
    public void Unquoted_query_matches_product_code_variants(string fileName, string query)
    {
        Assert.True(DmmPendingFileNameFilter.Matches(fileName, query));
    }

    [Theory]
    [InlineData("abcd-123.mp4", "abcd-12")]
    [InlineData("abcd-123.mp4", "abc")]
    [InlineData("1abcd00123.mp4", "abcd-12")]
    [InlineData("ABCD_0123.mp4", "abcd-1")]
    [InlineData("日本語タイトルのみのサンプル.mp4", "日本語")]
    [InlineData("ただの日本語タイトル.mp4", "ただの")]
    public void Unquoted_query_matches_compact_substring(string fileName, string query)
    {
        Assert.True(DmmPendingFileNameFilter.Matches(fileName, query));
    }

    [Theory]
    [InlineData("efgh-456.mp4", "abcd-123")]
    [InlineData("日本語タイトルのみのサンプル.mp4", "abcd-123")]
    [InlineData("abcd-123.mp4", "日本語")]
    public void Unrelated_names_do_not_match(string fileName, string query)
    {
        Assert.False(DmmPendingFileNameFilter.Matches(fileName, query));
    }
}
