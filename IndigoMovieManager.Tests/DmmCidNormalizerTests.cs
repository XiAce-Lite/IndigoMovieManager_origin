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
}
