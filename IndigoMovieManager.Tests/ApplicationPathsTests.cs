using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ApplicationPathsTests
{
    [Fact]
    public void ResolveThumbRoot_uses_application_base_when_thumb_folder_empty()
    {
        string root = ApplicationPaths.ResolveThumbRoot("mydb", "");
        Assert.StartsWith(ApplicationPaths.ApplicationBase, root, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Thumb", "mydb"), root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetThumbFileName_normalizes_movie_body_to_lowercase()
    {
        Assert.Equal("flash_2026-05-12-19.#abc.jpg", ThumbnailLayoutCache.GetThumbFileName("FLASH_2026-05-12-19", "abc"));
    }
}
