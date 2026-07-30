namespace IndigoMovieManager.Services
{
    internal static class PreviewPlaybackTiming
    {
        public const double DefaultFps = 30d;
        public const double MaxPreviewFps = 60d;

        public static double NormalizeFps(double fps, double maxFps = MaxPreviewFps)
        {
            if (fps <= 0 || double.IsNaN(fps) || double.IsInfinity(fps))
            {
                return DefaultFps;
            }

            return Math.Clamp(fps, 1d, maxFps);
        }

        public static TimeSpan GetTimerInterval(double fps) =>
            TimeSpan.FromMilliseconds(1000d / NormalizeFps(fps));

        public static double ClampSeekMs(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            return Math.Clamp(value, minimum, maximum);
        }

        public static int ClampSeekMs(int value, int delta, int minimum, int maximum) =>
            (int)ClampSeekMs(value + delta, minimum, maximum);
    }
}
