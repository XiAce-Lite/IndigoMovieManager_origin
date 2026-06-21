using System.Diagnostics;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// 長尺動画向けにサムネイルのサンプリング区間を決める。
    /// </summary>
    internal static class ThumbnailSamplingPolicy
    {
        /// <summary>この秒数を超える自動生成は先頭付近だけをサンプリングする。</summary>
        public const double LongDurationThresholdSec = 3600d;

        /// <summary>長尺時に使う仮想サンプリング幅（秒）。</summary>
        public const double VirtualDurationWindowSec = 300d;

        public static double GetEffectiveSamplingDuration(double durationSec, bool isManual)
        {
            if (isManual || durationSec <= 0d)
            {
                return durationSec;
            }

            if (durationSec <= LongDurationThresholdSec)
            {
                return durationSec;
            }

            return Math.Min(durationSec, VirtualDurationWindowSec);
        }

        public static bool UsesVirtualDurationWindow(double durationSec, bool isManual)
        {
            return !isManual
                && durationSec > LongDurationThresholdSec;
        }

        public static void LogVirtualDurationIfNeeded(
            string movieFullPath,
            double durationSec,
            bool isManual)
        {
            if (!UsesVirtualDurationWindow(durationSec, isManual))
            {
                return;
            }

            Debug.WriteLine(
                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] long-duration sampling: "
                + $"path='{movieFullPath}', duration_sec={durationSec:0}, "
                + $"window_sec={VirtualDurationWindowSec:0}"
            );
        }
    }
}
