using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// ffmpeg 1 パス tile（-ss ThumbSec[0] + fps=1/interval + tile）の適用可否。
    /// 均等配置の自動サンプリング（divideSec）向け。
    /// </summary>
    internal static class FfmpegOnePassPolicy
    {
        /// <summary>
        /// fps=1/interval による順デコードの上限（秒）。
        /// 超えると最終パネルまで尺の大半をデコードするため one-pass は使わない。
        /// </summary>
        internal const double MaxDecodeSpanSec = 900;

        public static bool CanUse(ThumbInfo thumbInfo, double durationSec)
        {
            if (thumbInfo?.ThumbSec == null || thumbInfo.ThumbSec.Count < 2)
            {
                return false;
            }

            IReadOnlyList<int> secList = thumbInfo.ThumbSec;
            if (secList[0] < 1)
            {
                return false;
            }

            int interval = secList[1] - secList[0];
            if (interval < 1)
            {
                return false;
            }

            for (int i = 2; i < secList.Count; i++)
            {
                if (secList[i] - secList[i - 1] != interval)
                {
                    return false;
                }
            }

            int panelCount = secList.Count;
            double decodeSpan = interval * (panelCount - 1);
            if (decodeSpan > MaxDecodeSpanSec)
            {
                return false;
            }

            if (durationSec > 0)
            {
                double maxNeeded = secList[0] + interval * (panelCount - 1);
                if (maxNeeded > durationSec + 1d)
                {
                    return false;
                }
            }

            return true;
        }

        public static double ResolveStartSec(ThumbInfo thumbInfo) =>
            Math.Max(0, thumbInfo.ThumbSec[0]);

        public static double ResolveIntervalSec(ThumbInfo thumbInfo, double durationSec)
        {
            if (thumbInfo?.ThumbSec != null && thumbInfo.ThumbSec.Count >= 2)
            {
                int interval = thumbInfo.ThumbSec[1] - thumbInfo.ThumbSec[0];
                if (interval > 0)
                {
                    return interval;
                }
            }

            int panelCount = thumbInfo?.ThumbSec?.Count ?? 0;
            if (durationSec > 0 && panelCount > 0)
            {
                double divide = durationSec / (panelCount + 1);
                if (divide > 0.1)
                {
                    return divide;
                }
            }

            return 1d;
        }
    }
}
