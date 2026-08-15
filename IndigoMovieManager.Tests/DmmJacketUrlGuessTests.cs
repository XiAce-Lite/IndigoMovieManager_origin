using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmJacketUrlGuessTests
{
    [Fact]
    public void CollectLiteralCid_keeps_h_underscore_prefix()
    {
        IReadOnlyList<string> cids = DmmJacketUrlGuess.CollectLiteralCid("h_000abcd00123");

        Assert.Equal(["h_000abcd00123"], cids);
    }

    [Fact]
    public void CollectLiteralCid_does_not_expand_or_strip_product_code()
    {
        Assert.Empty(DmmJacketUrlGuess.CollectLiteralCid("abcd-123"));
        Assert.Equal(["abcd00123"], DmmJacketUrlGuess.CollectLiteralCid("ABCD00123"));
    }

    [Fact]
    public void BuildRowsFromKeyword_includes_video_and_mono_templates()
    {
        IReadOnlyList<DmmJacketGuessRow> rows = DmmJacketUrlGuess.BuildRowsFromKeyword("h_000abcd00123");

        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal("h_000abcd00123", r.Cid));
        Assert.Equal("aws-video", rows[0].HostLabel);
        Assert.Equal(
            "https://awsimgsrc.dmm.co.jp/pics_dig/digital/video/h_000abcd00123/h_000abcd00123pl.jpg",
            rows[0].Url);
        Assert.Equal("pics-video", rows[1].HostLabel);
        Assert.Equal(
            "https://pics.dmm.co.jp/digital/video/h_000abcd00123/h_000abcd00123pl.jpg",
            rows[1].Url);
        Assert.Equal("aws-mono", rows[2].HostLabel);
        Assert.Equal(
            "https://awsimgsrc.dmm.co.jp/pics_dig/mono/movie/adult/h_000abcd00123/h_000abcd00123pl.jpg",
            rows[2].Url);
        Assert.Equal("pics-mono", rows[3].HostLabel);
        Assert.Equal(
            "https://pics.dmm.co.jp/mono/movie/adult/h_000abcd00123/h_000abcd00123pl.jpg",
            rows[3].Url);
    }

    [Fact]
    public void BuildUrlsFromKeyword_skips_when_empty_or_invalid()
    {
        Assert.Empty(DmmJacketUrlGuess.BuildUrlsFromKeyword(""));
        Assert.Empty(DmmJacketUrlGuess.BuildUrlsFromKeyword("ただの日本語タイトル"));
        Assert.Empty(DmmJacketUrlGuess.BuildUrlsFromKeyword("abcd-123"));
    }

    [Fact]
    public void IsPathSafeCid_allows_underscore_rejects_hyphen()
    {
        Assert.True(DmmJacketUrlGuess.IsPathSafeCid("abcd00123"));
        Assert.True(DmmJacketUrlGuess.IsPathSafeCid("h_000abcd00123"));
        Assert.False(DmmJacketUrlGuess.IsPathSafeCid("abcd-123"));
        Assert.False(DmmJacketUrlGuess.IsPathSafeCid(""));
        Assert.False(DmmJacketUrlGuess.IsPathSafeCid(null));
    }

    [Fact]
    public void GuessRow_Url_is_editable()
    {
        var row = new DmmJacketGuessRow { Cid = "abcd00123", HostLabel = "custom", Url = "https://example.test/a.jpg" };
        row.Url = "https://example.test/b.jpg";
        Assert.Equal("https://example.test/b.jpg", row.Url);
    }
}
