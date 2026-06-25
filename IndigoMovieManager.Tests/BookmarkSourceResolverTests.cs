using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class BookmarkSourceResolverTests
{
  [Fact]
  public void FindMovieRecordByPath_matches_full_path_case_insensitively()
  {
    var records = new[]
    {
      new MovieRecords { Movie_Id = 1, Movie_Path = @"D:\movies\a\sample.mp4" },
      new MovieRecords { Movie_Id = 2, Movie_Path = @"D:\movies\b\sample.mp4" },
    };

    MovieRecords found = BookmarkSourceResolver.FindMovieRecordByPath(
      records,
      @"D:\movies\b\sample.mp4");

    Assert.NotNull(found);
    Assert.Equal(2, found.Movie_Id);
  }

  [Fact]
  public void ResolveSourceMoviePath_prefers_stored_comment1_over_duplicate_names()
  {
    var library =
      new[]
      {
        new MovieRecords { Movie_Id = 1, Movie_Body = "sample", Movie_Path = @"D:\a\sample.mp4" },
        new MovieRecords { Movie_Id = 2, Movie_Body = "sample", Movie_Path = @"D:\b\sample.mp4" },
      };

    var bookmark = new MovieRecords
    {
      Movie_Body = "sample",
      Comment1 = @"D:\b\sample.mp4",
    };

    string path = BookmarkSourceResolver.ResolveSourceMoviePath(bookmark, library);

    Assert.Equal(@"D:\b\sample.mp4", path);
  }

  [Fact]
  public void ResolveSourceMoviePath_uses_hash_when_comment1_missing_and_names_duplicate()
  {
    var library =
      new[]
      {
        new MovieRecords { Movie_Id = 1, Movie_Body = "sample", Movie_Path = @"D:\a\sample.mp4", Hash = "aaa" },
        new MovieRecords { Movie_Id = 2, Movie_Body = "sample", Movie_Path = @"D:\b\sample.mp4", Hash = "bbb" },
      };

    var bookmark = new MovieRecords
    {
      Movie_Body = "sample",
      Hash = "bbb",
    };

    string path = BookmarkSourceResolver.ResolveSourceMoviePath(bookmark, library);

    Assert.Equal(@"D:\b\sample.mp4", path);
  }

  [Fact]
  public void ResolveSourceMoviePath_returns_null_when_duplicate_names_are_ambiguous()
  {
    var library =
      new[]
      {
        new MovieRecords { Movie_Id = 1, Movie_Body = "sample", Movie_Path = @"D:\a\sample.mp4" },
        new MovieRecords { Movie_Id = 2, Movie_Body = "sample", Movie_Path = @"D:\b\sample.mp4" },
      };

    var bookmark = new MovieRecords { Movie_Body = "sample" };

    string path = BookmarkSourceResolver.ResolveSourceMoviePath(bookmark, library);

    Assert.Null(path);
  }
}
