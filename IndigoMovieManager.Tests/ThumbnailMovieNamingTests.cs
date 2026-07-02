using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailMovieNamingTests
{
    [Fact]
    public void GetMovieBody_prefers_movie_path_over_movie_name()
    {
        var item = new MovieRecords
        {
            Movie_Name = "wrong.avi.avi",
            Movie_Path = @"F:\Temp\013011-605-carib-AVI.avi",
            Hash = "abc",
        };

        Assert.Equal("013011-605-carib-avi", ThumbnailMovieNaming.GetMovieBody(item));
    }

    [Fact]
    public void GetThumbFileName_matches_creation_orchestrator_convention()
    {
        var item = new MovieRecords
        {
            Movie_Name = "sample.avi.avi",
            Movie_Path = @"F:\Temp\sample.avi",
            Hash = "deadbeef",
        };

        Assert.Equal(
            "sample.#deadbeef.jpg",
            ThumbnailMovieNaming.GetThumbFileName(item));
    }
}
