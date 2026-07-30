namespace IndigoMovieManager.Services
{
    /// <summary>
    /// マニュアルサムネキャプチャのキュー投入用ヘルパ。
    /// </summary>
    internal static class ManualThumbnailCaptureFactory
    {
        public const int EnqueueRetryCount = 120;
        public const int EnqueueRetryDelayMs = 500;

        public const string BusyMessage =
            "サムネイル作成が混み合っています。しばらくしてから再度お試しください。";

        public static QueueObj Create(
            long movieId,
            string movieFullPath,
            int thumbPanelPos,
            int thumbTimePosSeconds)
        {
            return new QueueObj
            {
                MovieId = movieId,
                MovieFullPath = movieFullPath,
                ThumbPanelPos = thumbPanelPos,
                ThumbTimePos = thumbTimePosSeconds,
                IsManual = true,
            };
        }
    }
}
