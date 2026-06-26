namespace IndigoMovieManager.Tests;

using IndigoMovieManager.Thumbnail;
using Xunit;

public class MovieListFilterTests
{
  private static MovieRecords CreateRecord(
    string name,
    string path,
    string tags = "",
    string hash = "abc")
  {
    return new MovieRecords
    {
      Movie_Name = name,
      Movie_Path = path,
      Tags = tags,
      Hash = hash,
      Tag = string.IsNullOrEmpty(tags)
        ? []
        : tags.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList(),
    };
  }

  [Fact]
  public void Build_ExactQuotedMatch_FiltersFields()
  {
    var source = new[]
    {
      CreateRecord("alpha.mp4", @"C:\a\alpha.mp4", "tag1"),
      CreateRecord("beta.mp4", @"C:\a\beta.mp4", "other"),
    };

    var result = MovieListFilter.Build(source, "\"alpha\"", "1");

    Assert.Single(result.Items);
    Assert.Equal("alpha.mp4", result.Items[0].Movie_Name);
    Assert.Equal(1, result.SearchCount);
  }

  [Fact]
  public void Build_Notag_FiltersUntaggedRecords()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", ""),
      CreateRecord("b.mp4", @"C:\b.mp4", "x"),
    };

    var result = MovieListFilter.Build(source, "{notag}", "1");

    Assert.Single(result.Items);
    Assert.Equal("a.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_Tag_FiltersTaggedRecords()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", ""),
      CreateRecord("b.mp4", @"C:\b.mp4", "x"),
      CreateRecord("c.mp4", @"C:\c.mp4", "   "),
    };

    var result = MovieListFilter.Build(source, "{tag}", "1");

    Assert.Single(result.Items);
    Assert.Equal("b.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_Nofile_FiltersMissingFiles()
  {
    string existingPath = Path.GetTempFileName();
    try
    {
      var source = new[]
      {
        CreateRecord("exists.mp4", existingPath),
        CreateRecord("missing.mp4", @"C:\missing\missing.mp4"),
      };

      var result = MovieListFilter.Build(source, "{nofile}", "1");

      Assert.Single(result.Items);
      Assert.Equal("missing.mp4", result.Items[0].Movie_Name);
    }
    finally
    {
      File.Delete(existingPath);
    }
  }

  [Fact]
  public void Build_Dup_FiltersDuplicateHashes()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", hash: "dup"),
      CreateRecord("b.mp4", @"C:\b.mp4", hash: "dup"),
      CreateRecord("c.mp4", @"C:\c.mp4", hash: "unique"),
    };

    var result = MovieListFilter.Build(source, "{dup}", "1");

    Assert.Equal(2, result.Items.Count);
    Assert.Equal(2, result.SearchCount);
  }

  [Fact]
  public void Build_AndSearch_RequiresAllTerms()
  {
    var source = new[]
    {
      CreateRecord("foo bar.mp4", @"C:\foo bar.mp4", "baz"),
      CreateRecord("foo only.mp4", @"C:\foo only.mp4", "other"),
    };

    var result = MovieListFilter.Build(source, "foo baz", "1");

    Assert.Single(result.Items);
    Assert.Equal("foo bar.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_Error_FiltersErrorThumbnailsForCurrentTab()
  {
    string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
    string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-filter-err-{Guid.NewGuid():N}");
    Directory.CreateDirectory(thumbRoot);
    try
    {
      File.WriteAllText(moviePath, "movie");

      var cache = new ThumbnailLayoutCache();
      cache.Refresh("testdb", thumbRoot, 5);

      var source = new[]
      {
        CreateRecord("error.mp4", moviePath, hash: "errhash"),
        CreateRecord("ok.mp4", moviePath, hash: "okhash"),
      };

      string okThumb = cache.GetExpectedThumbPath(0, "ok", "okhash");
      string directory = Path.GetDirectoryName(okThumb);
      Directory.CreateDirectory(directory!);
      byte[] composite = new byte[128];
      BitConverter.GetBytes((ushort)1).CopyTo(composite, composite.Length - 60);
      File.WriteAllBytes(okThumb, composite);

      var context = new MovieListFilterContext
      {
        CurrentTabIndex = 0,
        ThumbnailCache = cache,
      };

      var result = MovieListFilter.Build(source, "{error}", "1", context);

      Assert.Single(result.Items);
      Assert.Equal("error.mp4", result.Items[0].Movie_Name);
    }
    finally
    {
      if (File.Exists(moviePath))
      {
        File.Delete(moviePath);
      }

      if (Directory.Exists(thumbRoot))
      {
        Directory.Delete(thumbRoot, true);
      }
    }
  }

  [Fact]
  public void Build_OrSearch_MatchesAnyGroup()
  {
    var source = new[]
    {
      CreateRecord("alpha.mp4", @"C:\alpha.mp4"),
      CreateRecord("beta.mp4", @"C:\beta.mp4"),
    };

    var result = MovieListFilter.Build(source, "alpha | beta", "1");

    Assert.Equal(2, result.Items.Count);
  }
}
