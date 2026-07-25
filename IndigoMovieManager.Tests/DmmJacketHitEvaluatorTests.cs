using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmJacketHitEvaluatorTests
{
    private static DmmCandidateEntry Entry(
        string contentId,
        string largeUrl = null,
        string productId = null,
        string title = null) =>
        new()
        {
            FloorLabel = "videoa",
            Item = new DmmItemDto
            {
                ContentId = contentId,
                ProductId = productId,
                Title = title ?? contentId,
                ImageUrl = largeUrl == null ? null : new DmmImageUrlDto { Large = largeUrl },
            },
        };

    private static string JacketUrl(string stub) =>
        $"https://pics.dmm.co.jp/{stub}.jpg";

    [Fact]
    public void TryConclude_applies_single_jacket_when_product_code_matches()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("a"),
            Entry("1abcd00123", JacketUrl("abcd123"), productId: "abcd-123"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123");

        Assert.Equal(DmmResolveOutcome.Applied, result.Outcome);
        Assert.Equal("1abcd00123", result.Item.ContentId);
    }

    [Fact]
    public void TryConclude_applies_when_number_padding_differs()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00170", JacketUrl("pad"), productId: "abcd-170"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-170");

        Assert.Equal(DmmResolveOutcome.Applied, result.Outcome);
    }

    [Fact]
    public void TryConclude_ambiguous_when_single_jacket_product_code_differs()
    {
        // 要求 abcd-170。ジャケありは別番号 abcd00107 のみ（類似キーワード誤爆の再現）。
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00107", JacketUrl("wrong"), productId: "abcd-107"),
            Entry("1abcd00170"),
            Entry("abcd170u"),
            Entry("1abcd00171"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-170");

        Assert.Equal(DmmResolveOutcome.Ambiguous, result.Outcome);
        Assert.Equal(4, result.Candidates.Count);
        Assert.Null(result.Item);
    }

    [Fact]
    public void TryConclude_ambiguous_when_multiple_jackets()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00123", JacketUrl("a"), productId: "abcd-123"),
            Entry("1abcd00124", JacketUrl("b"), productId: "abcd-124"),
            Entry("c"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123");

        Assert.Equal(DmmResolveOutcome.Ambiguous, result.Outcome);
        Assert.Equal(3, result.Candidates.Count);
    }

    [Fact]
    public void TryConclude_returns_null_when_no_jacket()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("a"),
            Entry("b"),
        };

        Assert.Null(DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123"));
        Assert.False(DmmJacketHitEvaluator.HasAnyUsableJacket(candidates));
    }

    [Fact]
    public void TryConclude_ignores_placeholder_jacket()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("a", "https://pics.dmm.com/mono/movie/n/now_printing/now_printing.jpg"),
        };

        Assert.Null(DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123"));
    }
}

public class DmmProductCodeMatcherTests
{
    [Theory]
    [InlineData("1abcd00107", "abcd-170", false)]
    [InlineData("1abcd00170", "abcd-170", true)]
    [InlineData("abcd00170", "abcd-170", true)]
    [InlineData("1abcd00170", "abcd-107", false)]
    public void ItemMatchesProductCode_compares_maker_and_number(
        string contentId,
        string productCode,
        bool expected)
    {
        var item = new DmmItemDto { ContentId = contentId, ProductId = contentId };
        Assert.Equal(expected, DmmProductCodeMatcher.ItemMatchesProductCode(item, productCode));
    }
}
