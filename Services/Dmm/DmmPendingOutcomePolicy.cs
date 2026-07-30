namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// 自動/一括取得で未確定候補 DB へ残す解決結果。
    /// </summary>
    internal static class DmmPendingOutcomePolicy
    {
        public static bool ShouldPersistPending(DmmResolveOutcome outcome) =>
            outcome is DmmResolveOutcome.Ambiguous
                or DmmResolveOutcome.NotFound
                or DmmResolveOutcome.NoProductCode;
    }
}
