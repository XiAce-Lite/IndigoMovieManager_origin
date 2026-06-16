namespace IndigoMovieManager.Thumbnail
{
    public sealed class ThumbnailCreateResult
    {
        public bool Success { get; init; }
        public IReadOnlyList<string> PanelPaths { get; init; } = [];
        public string FailureReason { get; init; } = "";

        public static ThumbnailCreateResult Succeeded(IReadOnlyList<string> panelPaths) =>
            new() { Success = true, PanelPaths = panelPaths };

        public static ThumbnailCreateResult Failed(string reason) =>
            new() { Success = false, FailureReason = reason };
    }
}
