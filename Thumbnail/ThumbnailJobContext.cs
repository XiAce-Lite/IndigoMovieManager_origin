namespace IndigoMovieManager.Thumbnail
{
    public sealed class ThumbnailJobContext
    {
        public QueueObj QueueObj { get; init; }
        public TabInfo TabInfo { get; init; }
        public string MovieFullPath { get; init; } = "";
        public string SaveThumbFileName { get; init; } = "";
        public string TempFileBody { get; init; } = "";
        public string TempPath { get; init; } = "";
        public string Hash { get; init; } = "";
        public bool IsManual { get; init; }
        public bool IsResizeThumb { get; init; }
    }
}
