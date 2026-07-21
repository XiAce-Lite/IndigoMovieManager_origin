namespace IndigoMovieManager.Services.Dmm
{
    internal enum DmmResolveOutcome
    {
        Applied,
        NoProductCode,
        NotFound,
        Ambiguous,
        HttpError,
        NotConfigured,
    }

    internal sealed class DmmResolveResult
    {
        public DmmResolveOutcome Outcome { get; init; }
        public DmmItemDto Item { get; init; }
        public string ProductCode { get; init; }
        public string Detail { get; init; }
        public string InitialKeyword { get; init; }
        public IReadOnlyList<DmmCandidateEntry> Candidates { get; init; } = [];

        public static DmmResolveResult Applied(DmmItemDto item, string productCode) =>
            new()
            {
                Outcome = DmmResolveOutcome.Applied,
                Item = item,
                ProductCode = productCode,
            };

        public static DmmResolveResult Ambiguous(
            IReadOnlyList<DmmCandidateEntry> candidates,
            string productCode,
            string initialKeyword) =>
            new()
            {
                Outcome = DmmResolveOutcome.Ambiguous,
                ProductCode = productCode,
                InitialKeyword = initialKeyword,
                Candidates = candidates ?? [],
                Detail = "複数候補",
            };

        public static DmmResolveResult Skip(DmmResolveOutcome outcome, string detail = null) =>
            new() { Outcome = outcome, Detail = detail };
    }
}
