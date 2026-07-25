using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

/// <summary>
/// 実 API 疎通。環境変数 IMM_DMM_LIVE_PRODUCT に品番を入れたときだけ実行する。
/// 例: $env:IMM_DMM_LIVE_PRODUCT='xxxx-000'; $env:IMM_DMM_API_ID='...'; $env:IMM_DMM_AFFILIATE_ID='...'
/// リポジトリには実品番・API キーを書かない。
/// </summary>
public class DmmLiveResolveTests
{
    [Fact]
    public async Task Live_resolve_similar_jacket_is_ambiguous_not_auto_applied()
    {
        string product = Environment.GetEnvironmentVariable("IMM_DMM_LIVE_PRODUCT")?.Trim();
        if (string.IsNullOrWhiteSpace(product))
        {
            return;
        }

        string apiId = Environment.GetEnvironmentVariable("IMM_DMM_API_ID")?.Trim()
            ?? IndigoMovieManager.Properties.Settings.Default.DmmApiId?.Trim();
        string affiliate = Environment.GetEnvironmentVariable("IMM_DMM_AFFILIATE_ID")?.Trim()
            ?? IndigoMovieManager.Properties.Settings.Default.DmmAffiliateId?.Trim();

        if (string.IsNullOrWhiteSpace(apiId) || string.IsNullOrWhiteSpace(affiliate))
        {
            return;
        }

        var options = new DmmApiOptions { ApiId = apiId, AffiliateId = affiliate };
        var resolver = new DmmMetadataResolveService(new DmmItemListClient(options));
        DmmResolveResult result = await resolver.ResolveAsync(product + ".mp4", CancellationToken.None);

        Assert.NotEqual(DmmResolveOutcome.Applied, result.Outcome);
        Assert.Equal(DmmResolveOutcome.Ambiguous, result.Outcome);
        Assert.True(result.Candidates.Count >= 2);

        int jacketCount = DmmJacketHitEvaluator.CountUsableJackets(result.Candidates);
        Assert.True(jacketCount >= 1);

        // ジャケあり候補は要求品番と番号が一致しない（誤爆防止の本命）
        foreach (DmmCandidateEntry entry in result.Candidates)
        {
            if (!DmmCandidateDisplay.HasUsableJacket(entry?.Item))
            {
                continue;
            }

            Assert.False(
                DmmProductCodeMatcher.ItemMatchesProductCode(entry.Item, product),
                "usable jacket candidate unexpectedly matched requested product code");
        }
    }
}
