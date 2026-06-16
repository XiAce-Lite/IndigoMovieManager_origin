using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal static class ThumbnailJobPreparer
    {
        public static bool TryBuildThumbInfo(
            ThumbnailJobContext ctx,
            double durationSec,
            out ThumbInfo thumbInfo
        )
        {
            thumbInfo = null;
            if (ctx == null) { return false; }

            int divideSec = (int)(durationSec / ((ctx.TabInfo.Columns * ctx.TabInfo.Rows) + 1));
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
            else
            {
                for (int i = 1; i < thumbInfo.ThumbCounts + 1; i++)
                {
                    thumbInfo.Add(i * divideSec);
                }
            }

            thumbInfo.NewThumbInfo();
            return true;
        }
    }
}
