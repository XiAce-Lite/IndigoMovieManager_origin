namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// OpenCV 共有キャプチャで、昇順 ThumbSec を順方向に読み進めるかどうかを決める。
    /// </summary>
    internal static class OpenCvForwardCapturePolicy
    {
        /// <summary>順方向 Grab の上限（この推定フレーム数を超えると PosMsec シークに切り替える）。</summary>
        public const int DefaultMaxForwardGrabs = 300;

        public static bool CanUseForwardCapture(ThumbnailJobContext ctx, IReadOnlyList<int> thumbSec)
        {
            if (ctx?.IsManual == true || thumbSec == null || thumbSec.Count < 2)
            {
                return false;
            }

            for (int i = 1; i < thumbSec.Count; i++)
            {
                if (thumbSec[i] < thumbSec[i - 1])
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ShouldForwardGrab(
            double currentMsec,
            double targetMsec,
            double fps,
            int maxForwardGrabs = DefaultMaxForwardGrabs)
        {
            if (targetMsec <= currentMsec + 50d)
            {
                return true;
            }

            double safeFps = fps > 0d && !double.IsNaN(fps) && !double.IsInfinity(fps) ? fps : 30d;
            int estimatedFrames = (int)Math.Ceiling((targetMsec - currentMsec) / 1000d * safeFps);
            return estimatedFrames <= maxForwardGrabs;
        }

        public static int EstimateForwardGrabCount(double currentMsec, double targetMsec, double fps)
        {
            if (targetMsec <= currentMsec + 50d)
            {
                return 0;
            }

            double safeFps = fps > 0d && !double.IsNaN(fps) && !double.IsInfinity(fps) ? fps : 30d;
            return (int)Math.Ceiling((targetMsec - currentMsec) / 1000d * safeFps);
        }
    }
}
