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
    public void TryConclude_continues_when_single_jacket_product_code_differs()
    {
        // 要求 abcd-170。ジャケありは別番号 abcd00107 のみ → 打ち切らず継続。
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00107", JacketUrl("wrong"), productId: "abcd-107"),
            Entry("1abcd00170"),
            Entry("abcd170u"),
            Entry("1abcd00171"),
        };

        Assert.Null(DmmJacketHitEvaluator.TryConclude(candidates, "abcd-170"));
    }

    [Fact]
    public void TryConclude_applies_when_one_matching_jacket_among_unrelated()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("noise1", JacketUrl("n1"), productId: "zzzz-001"),
            Entry("h_1615abcd00123", JacketUrl("ok"), productId: "abcd123"),
            Entry("noise2", JacketUrl("n2"), productId: "yyyy-002"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123");

        Assert.Equal(DmmResolveOutcome.Applied, result.Outcome);
        Assert.Equal("h_1615abcd00123", result.Item.ContentId);
    }

    [Fact]
    public void TryConclude_ambiguous_when_multiple_matching_jackets()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00123", JacketUrl("a"), productId: "abcd-123"),
            Entry("h_99abcd00123", JacketUrl("b"), productId: "abcd00123"),
            Entry("c"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123");

        Assert.Equal(DmmResolveOutcome.Ambiguous, result.Outcome);
        Assert.Equal(3, result.Candidates.Count);
    }

    [Fact]
    public void TryConclude_applies_when_unrelated_and_one_matching_among_two_jackets()
    {
        // 以前はジャケ2件で即 Ambiguous。一致1件なら Applied。
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("1abcd00123", JacketUrl("a"), productId: "abcd-123"),
            Entry("1abcd00124", JacketUrl("b"), productId: "abcd-124"),
            Entry("c"),
        };

        DmmResolveResult result = DmmJacketHitEvaluator.TryConclude(candidates, "abcd-123");

        Assert.Equal(DmmResolveOutcome.Applied, result.Outcome);
        Assert.Equal("1abcd00123", result.Item.ContentId);
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

    [Fact]
    public void TryConclude_continues_when_matching_id_has_no_jacket()
    {
        var candidates = new List<DmmCandidateEntry>
        {
            Entry("noise", JacketUrl("n"), productId: "zzzz-001"),
            Entry("h_796abcd00074"),
        };

        Assert.Null(DmmJacketHitEvaluator.TryConclude(candidates, "abcd-074"));
    }
}

public class DmmProductCodeMatcherTests
{
    [Theory]
    [InlineData("1abcd00107", "abcd-170", false)]
    [InlineData("1abcd00170", "abcd-170", true)]
    [InlineData("abcd00170", "abcd-170", true)]
    [InlineData("1abcd00170", "abcd-107", false)]
    [InlineData("h_1615abcd00123", "abcd-123", true)]
    [InlineData("24abcd00123", "abcd-123", true)]
    [InlineData("h_1615abcd00330", "abcd-033", false)]
    [InlineData("h_1615abcd00033", "abcd-033", true)]
    [InlineData("24efgh00017", "efgh-017", true)]
    public void ItemMatchesProductCode_compares_maker_and_number(
        string contentId,
        string productCode,
        bool expected)
    {
        var item = new DmmItemDto { ContentId = contentId, ProductId = contentId };
        Assert.Equal(expected, DmmProductCodeMatcher.ItemMatchesProductCode(item, productCode));
    }

    [Fact]
    public void BuildPadded5Keyword_zero_pads_number()
    {
        Assert.Equal("abcd00123", DmmProductCodeMatcher.BuildPadded5Keyword("abcd-123"));
        Assert.Equal("abcd00074", DmmProductCodeMatcher.BuildPadded5Keyword("abcd-074"));
    }
}
