using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
    internal sealed class MovieListFilterContext
    {
        public SkinEngine CurrentSkinEngine { get; init; } = SkinEngine.Wpf;

        public ThumbnailLayoutCache ThumbnailCache { get; init; }

        public string DbFullPath { get; init; }
    }
}
