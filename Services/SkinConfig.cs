namespace IndigoMovieManager.Services
{
    internal sealed class SkinConfig
    {
        public int SkinVersion { get; init; } = 1;
        public int ThumbWidth { get; init; } = 120;
        public int ThumbHeight { get; init; } = 90;
        public int ThumbColumn { get; init; } = 3;
        public int ThumbRow { get; init; } = 1;
        public int MultiSelect { get; init; } = 1;
        public int SeamlessScroll { get; init; }
        public string ScrollId { get; init; } = "view";

        public int PanelCount => ThumbColumn * ThumbRow;
        public int SheetWidth => ThumbWidth * ThumbColumn;
        public int SheetHeight => ThumbHeight * ThumbRow;

        public static SkinConfig DefaultSmallWeb() => new()
        {
            ThumbWidth = 120,
            ThumbHeight = 90,
            ThumbColumn = 3,
            ThumbRow = 1,
            MultiSelect = 1,
            SeamlessScroll = 0,
        };

        public static SkinConfig DefaultGridWeb() => new()
        {
            ThumbWidth = 160,
            ThumbHeight = 120,
            ThumbColumn = 1,
            ThumbRow = 1,
            MultiSelect = 1,
            SeamlessScroll = 0,
        };

        public bool Matches(SkinConfig expected) =>
            ThumbWidth == expected.ThumbWidth
            && ThumbHeight == expected.ThumbHeight
            && ThumbColumn == expected.ThumbColumn
            && ThumbRow == expected.ThumbRow;

        public SkinConfig WithFallback(SkinConfig fallback) => new()
        {
            SkinVersion = SkinVersion,
            ThumbWidth = ThumbWidth > 0 ? ThumbWidth : fallback.ThumbWidth,
            ThumbHeight = ThumbHeight > 0 ? ThumbHeight : fallback.ThumbHeight,
            ThumbColumn = ThumbColumn > 0 ? ThumbColumn : fallback.ThumbColumn,
            ThumbRow = ThumbRow > 0 ? ThumbRow : fallback.ThumbRow,
            MultiSelect = MultiSelect,
            SeamlessScroll = SeamlessScroll,
            ScrollId = string.IsNullOrWhiteSpace(ScrollId) ? fallback.ScrollId : ScrollId,
        };
    }
}
