using IndigoMovieManager;
using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmPendingOutcomePolicyTests
{
    [Fact]
    public void ShouldPersistPending_true_for_ambiguous_not_found_no_code()
    {
        Assert.True(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.Ambiguous));
        Assert.True(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.NotFound));
        Assert.True(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.NoProductCode));
    }

    [Fact]
    public void ShouldPersistPending_false_for_http_error_and_applied()
    {
        Assert.False(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.HttpError));
        Assert.False(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.Applied));
        Assert.False(DmmPendingOutcomePolicy.ShouldPersistPending(DmmResolveOutcome.NotConfigured));
    }
}

public class DmmResolveResultSkipTests
{
    [Fact]
    public void Skip_keeps_initial_keyword_for_not_found()
    {
        DmmResolveResult result = DmmResolveResult.Skip(
            DmmResolveOutcome.NotFound,
            "未ヒット",
            "abcd-123");

        Assert.Equal(DmmResolveOutcome.NotFound, result.Outcome);
        Assert.Equal("abcd-123", result.InitialKeyword);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void Skip_keeps_initial_keyword_for_no_product_code()
    {
        DmmResolveResult result = DmmResolveResult.Skip(
            DmmResolveOutcome.NoProductCode,
            "品番なし",
            "ただの日本語タイトル");

        Assert.Equal(DmmResolveOutcome.NoProductCode, result.Outcome);
        Assert.Equal("ただの日本語タイトル", result.InitialKeyword);
    }
}

public class DmmItemListClientQueryTests
{
    [Fact]
    public void BuildKeywordSearchQuery_includes_offset_and_hits()
    {
        string query = DmmItemListClient.BuildKeywordSearchQuery(
            "api",
            "aff-990",
            "abcd-123",
            hits: 10,
            offset: 11);

        Assert.Contains("offset=11", query, StringComparison.Ordinal);
        Assert.Contains("hits=10", query, StringComparison.Ordinal);
        Assert.Contains("keyword=abcd-123", query, StringComparison.Ordinal);
        Assert.DoesNotContain("cid=", query, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildKeywordSearchQuery_clamps_offset_to_at_least_one()
    {
        string query = DmmItemListClient.BuildKeywordSearchQuery(
            "api",
            "aff-990",
            "kw",
            hits: 10,
            offset: 0);

        Assert.Contains("offset=1", query, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCidSearchQuery_includes_offset_hits_and_cid()
    {
        string query = DmmItemListClient.BuildCidSearchQuery(
            "api",
            "aff-990",
            "abcd123",
            "digital",
            "videoa",
            hits: 10,
            offset: 21);

        Assert.Contains("offset=21", query, StringComparison.Ordinal);
        Assert.Contains("hits=10", query, StringComparison.Ordinal);
        Assert.Contains("cid=abcd123", query, StringComparison.Ordinal);
        Assert.Contains("service=digital", query, StringComparison.Ordinal);
        Assert.Contains("floor=videoa", query, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCidSearchQuery_supports_amateur_videoc_floor()
    {
        string query = DmmItemListClient.BuildCidSearchQuery(
            "api",
            "aff-990",
            "abcd418",
            "digital",
            "videoc",
            hits: 10,
            offset: 1);

        Assert.Contains("service=digital", query, StringComparison.Ordinal);
        Assert.Contains("floor=videoc", query, StringComparison.Ordinal);
        Assert.Contains("cid=abcd418", query, StringComparison.Ordinal);
    }
}

public class DmmPendingNotFoundStoreTests : IDisposable
{
    private readonly string _dbPath;

    public DmmPendingNotFoundStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"imm-dmm-pending-{Guid.NewGuid():N}.wb");
        SQLite.CreateDatabase(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public void Save_empty_candidates_for_not_found_style_pending()
    {
        DmmPendingCandidateStore.Save(
            _dbPath,
            77,
            "abcd-123.mp4",
            "abcd-123",
            [],
            "bulk");

        List<DmmPendingCandidateRecord> listed = DmmPendingCandidateStore.List(_dbPath);
        Assert.Single(listed);
        Assert.Equal(77, listed[0].MovieId);
        Assert.Equal("abcd-123", listed[0].InitialKeyword);
        Assert.Empty(listed[0].Candidates);
        Assert.Equal("bulk", listed[0].Source);
    }
}
