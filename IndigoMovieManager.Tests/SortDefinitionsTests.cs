namespace IndigoMovieManager.Tests;

using Xunit;

public class SortDefinitionsTests
{
    [Theory]
    [InlineData("0", "last_date desc")]
    [InlineData("1", "last_date")]
    [InlineData("6", "Score desc")]
    [InlineData("27", "comment3 desc")]
    [InlineData("28", "movie_id")]
    [InlineData("99", "")]
    public void GetSqlOrderClause_ReturnsExpectedClause(string id, string expected)
    {
        Assert.Equal(expected, SortDefinitions.GetSqlOrderClause(id));
    }

    [Fact]
    public void Apply_SortsByMovieNameAscending()
    {
        var records = new[]
        {
            new MovieRecords { Movie_Name = "b.mp4" },
            new MovieRecords { Movie_Name = "a.mp4" },
        };

        var sorted = SortDefinitions.Apply("12", records).Select(x => x.Movie_Name).ToArray();

        Assert.Equal(["a.mp4", "b.mp4"], sorted);
    }

    [Fact]
    public void Apply_Random_IsDeterministicForSameSeed()
    {
        var records = new[]
        {
            new MovieRecords { Movie_Id = 1, Movie_Name = "a.mp4" },
            new MovieRecords { Movie_Id = 2, Movie_Name = "b.mp4" },
            new MovieRecords { Movie_Id = 3, Movie_Name = "c.mp4" },
            new MovieRecords { Movie_Id = 4, Movie_Name = "d.mp4" },
        };

        SortDefinitions.ReseedRandom();
        long[] first = SortDefinitions.Apply("28", records).Select(x => x.Movie_Id).ToArray();
        long[] second = SortDefinitions.Apply("28", records).Select(x => x.Movie_Id).ToArray();
        Assert.Equal(first, second);

        SortDefinitions.ReseedRandom();
        long[] third = SortDefinitions.Apply("28", records).Select(x => x.Movie_Id).ToArray();
        // シード変更後は順序が変わりうる（偶然一致は許容し、少なくとも同一シードの決定性は上で検証）
        Assert.Equal(4, third.Length);
        Assert.Equal(first.Order().ToArray(), third.Order().ToArray());
    }
}
