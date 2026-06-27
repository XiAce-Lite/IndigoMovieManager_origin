using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
    internal sealed class MovieListFilterContext
    {
        public int CurrentTabIndex { get; init; } = -1;

        public ThumbnailLayoutCache ThumbnailCache { get; init; }

        public string DbFullPath { get; init; }
    }
}
