using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailJobPreparer
    {
        private const int UnknownDurationIntervalSec = 10;

        public static bool TryBuildThumbInfo(
            ThumbnailJobContext ctx,
            double durationSec,
            out ThumbInfo thumbInfo
        )
        {
            thumbInfo = null;
            if (ctx == null) { return false; }

            thumbInfo = new ThumbInfo
            {
                ThumbWidth = ctx.TabInfo.Width,
                ThumbHeight = ctx.TabInfo.Height,
                ThumbRows = ctx.TabInfo.Rows,
                ThumbColumns = ctx.TabInfo.Columns,
                ThumbCounts = ctx.TabInfo.Columns * ctx.TabInfo.Rows,
            };

            if (ctx.IsManual)
            {
                thumbInfo.GetThumbInfo(ctx.SaveThumbFileName);
                if (thumbInfo.IsThumbnail == false) { return false; }

                if (ctx.QueueObj.ThumbPanelPos.HasValue && ctx.QueueObj.ThumbTimePos.HasValue)
                {
                    thumbInfo.ThumbSec[(int)ctx.QueueObj.ThumbPanelPos] = (int)ctx.QueueObj.ThumbTimePos;
                }
            }
            else if (durationSec > 0)
            {
                double samplingDuration = ThumbnailSamplingPolicy.GetEffectiveSamplingDuration(
                    durationSec,
                    ctx.IsManual);

                int divideSec = (int)(samplingDuration / (thumbInfo.ThumbCounts + 1));
                if (divideSec < 1)
                {
                    divideSec = 1;
                }

                for (int i = 1; i < thumbInfo.ThumbCounts + 1; i++)
                {
                    thumbInfo.Add(i * divideSec);
                }
            }
            else
            {
                for (int i = 0; i < thumbInfo.ThumbCounts; i++)
                {
                    thumbInfo.Add(i * UnknownDurationIntervalSec);
                }
            }

            thumbInfo.NewThumbInfo();
            return true;
        }
    }
}
