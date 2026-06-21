namespace IndigoMovieManager.Tests;

using Xunit;

public class SortDefinitionsTests
{
    [Theory]
    [InlineData("0", "last_date desc")]
    [InlineData("1", "last_date")]
    [InlineData("6", "Score desc")]
    [InlineData("27", "comment3 desc")]
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
}
