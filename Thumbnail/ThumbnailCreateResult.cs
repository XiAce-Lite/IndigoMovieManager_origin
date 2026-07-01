namespace IndigoMovieManager.Thumbnail
{
    public sealed class ThumbnailCreateResult
    {
        public bool Success { get; init; }
        public IReadOnlyList<string> PanelPaths { get; init; } = [];
        public string FailureReason { get; init; } = "";
        /// <summary>サムネ生成に使ったバックエンド（OpenCV / FFmpeg / ZIP など）。</summary>
        public string Backend { get; init; } = "";
        /// <summary>FFmpeg 時は hwaccel 名または software。OpenCV 時は OpenCV。</summary>
        public string Decoder { get; init; } = "";

        public static ThumbnailCreateResult Succeeded(
            IReadOnlyList<string> panelPaths,
            string backend = "",
            string decoder = "") =>
            new()
            {
                Success = true,
                PanelPaths = panelPaths,
                Backend = backend ?? "",
                Decoder = decoder ?? "",
            };

        public static ThumbnailCreateResult Failed(string reason, string backend = "", string decoder = "") =>
            new()
            {
                Success = false,
                FailureReason = reason,
                Backend = backend ?? "",
                Decoder = decoder ?? "",
            };
    }
}
