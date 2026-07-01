namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 重複パネル検出時に OpenCV の per-panel 二巡を行うか。
    /// 自動生成では FFmpeg フォールバックへ任せる（B-6）。
    /// </summary>
    internal static class ThumbnailDuplicateRetryPolicy
    {
        public static bool ShouldRetryOpenCvPerPanel(bool isManual) => isManual;
    }
}
