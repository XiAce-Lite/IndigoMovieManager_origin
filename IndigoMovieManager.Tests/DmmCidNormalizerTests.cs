using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmCidNormalizerTests
{
    [Theory]
    [InlineData("abcd-123.mp4", "abcd-123", "1abcd00123")]
    [InlineData("ABCD-123", "abcd-123", "1abcd00123")]
    [InlineData("abcd123.mp4", "abcd-123", "1abcd00123")]
    [InlineData("EFGH-456.mkv", "efgh-456", "efgh456")]
    [InlineData("efgh456", "efgh-456", "efgh456")]
    [InlineData("abcd-123_extra.mp4", "abcd-123", "1abcd00123")]
    [InlineData("abcd-123a.mp4", "abcd-123", "1abcd00123")]
    [InlineData("xxxx-024b.mp4", "xxxx-024", "xxxx024")]
    [InlineData("529abcd-123.mp4", "abcd-123", "529abcd00123")]
    [InlineData("118efgh-456", "efgh-456", "118efgh00456")]
    public void ExtractFromFileName_builds_expected_candidates(
        string fileName,
        string expectedProductCode,
        string expectedCid)
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName(fileName);

        Assert.True(result.HasProductCode);
        Assert.Equal(expectedProductCode, result.ProductCode);
        Assert.Contains(expectedCid, result.CidCandidates);
        Assert.Equal(expectedProductCode.Replace('-', ' '), result.SpaceForm);
    }

    [Fact]
    public void ExtractFromFileName_keeps_channel_prefix_and_space_form()
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName("529abcd-123.mp4");

        Assert.Equal("abcd-123", result.ProductCode);
        Assert.Equal("abcd 123", result.SpaceForm);
        Assert.Equal("529", result.ChannelPrefix);
        Assert.Equal("529abcd00123", result.CidCandidates[0]);
    }

    [Fact]
    public void ExtractFromFileName_keeps_branch_letter_separately()
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName("abcd-123b.mp4");

        Assert.Equal("abcd-123", result.ProductCode);
        Assert.Equal("b", result.BranchLetter);
        Assert.Equal("abcd-123b", result.ProductCodeWithBranch);
        Assert.Equal("abcd 123", result.SpaceForm);
    }

    [Theory]
    [InlineData("日本語タイトルのみのサンプル.mp4")]
    [InlineData("ただの日本語タイトル.mp4")]
    [InlineData("")]
    [InlineData(null)]
    public void ExtractFromFileName_returns_empty_when_no_product_code(string fileName)
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName(fileName);

        Assert.False(result.HasProductCode);
        Assert.Empty(result.CidCandidates);
    }

    [Fact]
    public void BuildCidCandidates_prefers_one_prefix_padded_form()
    {
        IReadOnlyList<string> candidates = DmmCidNormalizer.BuildCidCandidates("abcd", "123");

        Assert.Equal("1abcd00123", candidates[0]);
        Assert.Contains("abcd00123", candidates);
        Assert.Contains("abcd-123", candidates);
    }

    [Fact]
    public void BuildCidCandidates_includes_six_digit_padded_forms()
    {
        IReadOnlyList<string> candidates = DmmCidNormalizer.BuildCidCandidates("abcd", "030");

        Assert.Contains("1abcd000030", candidates);
        Assert.Contains("abcd000030", candidates);
        Assert.Contains("abcd-000030", candidates);
    }

    [Theory]
    [InlineData("1abcd000030", "abcd-030", "1abcd000030")]
    [InlineData("abcd000030", "abcd-030", "abcd000030")]
    [InlineData("529abcd00123", "abcd-123", "529abcd00123")]
    [InlineData("h_000abcd00123", "abcd-123", "h_000abcd00123")]
    [InlineData("h_491abcd00022", "abcd-022", "h_491abcd00022")]
    public void ExtractFromSearchInput_accepts_direct_content_id(
        string searchInput,
        string expectedProductCode,
        string expectedFirstCid)
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromSearchInput(searchInput);

        Assert.True(result.HasProductCode);
        Assert.Equal(expectedProductCode, result.ProductCode);
        Assert.Equal(expectedFirstCid, result.CidCandidates[0]);
        Assert.Contains(expectedFirstCid, result.CidCandidates);
    }

    [Theory]
    [InlineData("https://video.dmm.co.jp/av/content/?id=h_491abcd00022", "h_491abcd00022", "abcd-022")]
    [InlineData("https://www.dmm.co.jp/digital/videoa/-/detail/=/cid=h_000abcd00123/", "h_000abcd00123", "abcd-123")]
    [InlineData("id=h_000abcd00123", "h_000abcd00123", "abcd-123")]
    public void ExtractFromSearchInput_extracts_cid_from_url(
        string input,
        string expectedLiteralCid,
        string expectedProductCode)
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromSearchInput(input);

        Assert.True(result.HasProductCode);
        Assert.Equal(expectedProductCode, result.ProductCode);
        Assert.Equal(expectedLiteralCid, result.LiteralCid);
        Assert.Equal(expectedLiteralCid, result.CidCandidates[0]);
        Assert.True(result.HasHUnderscorePrefix);
    }

    [Fact]
    public void ExtractFromFileName_builds_stripped_keyword_for_leading_zeros()
    {
        DmmCidNormalizer.ExtractResult result = DmmCidNormalizer.ExtractFromFileName("abcd-022.mp4");

        Assert.Equal("abcd-022", result.ProductCode);
        Assert.Equal("abcd-22", result.StrippedProductCode);
        Assert.Equal("abcd 22", result.StrippedSpaceForm);
        Assert.Contains("abcd22", result.CidCandidates);
        Assert.Contains(
            "abcd-22",
            DmmCidNormalizer.BuildExtraKeywordVariants(result));
    }

    [Fact]
    public void ExtractFromFileName_accepts_h_underscore_content_id_file()
    {
        DmmCidNormalizer.ExtractResult result =
            DmmCidNormalizer.ExtractFromFileName("h_491abcd00022.mp4");

        Assert.Equal("abcd-022", result.ProductCode);
        Assert.Equal("h_491abcd00022", result.CidCandidates[0]);
        Assert.True(result.HasHUnderscorePrefix);
        Assert.Equal("491", result.ChannelPrefix);
    }

    [Fact]
    public void BuildCidCandidates_includes_h_underscore_when_requested()
    {
        IReadOnlyList<string> candidates =
            DmmCidNormalizer.BuildCidCandidates("abcd", "022", "491", includeHUnderscore: true);

        Assert.Equal("h_491abcd00022", candidates[0]);
        Assert.Contains("491abcd00022", candidates);
    }

    [Fact]
    public void StripLeadingZeros_does_not_repad()
    {
        Assert.Equal("22", DmmCidNormalizer.StripLeadingZeros("022"));
        Assert.Equal("22", DmmCidNormalizer.StripLeadingZeros("00022"));
        Assert.Equal("0", DmmCidNormalizer.StripLeadingZeros("000"));
    }
}
