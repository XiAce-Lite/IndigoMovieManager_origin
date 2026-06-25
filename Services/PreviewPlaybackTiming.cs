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
    }
}
