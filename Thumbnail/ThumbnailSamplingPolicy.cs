namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// サムネイルのサンプリング区間を決める。
    /// </summary>
    internal static class ThumbnailSamplingPolicy
    {
        /// <summary>動画長が不明なときの ffmpeg 単一フレーム探索幅（秒）。</summary>
        public const double UnknownDurationSeekWindowSec = 300d;

        public static double GetEffectiveSamplingDuration(double durationSec, bool isManual)
        {
            return durationSec;
        }
    }
}
