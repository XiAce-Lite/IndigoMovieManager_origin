namespace IndigoMovieManager.Tests;

using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Xunit;

public class MovieListFilterTests
{
  private static MovieRecords CreateRecord(
    string name,
    string path,
    string tags = "",
    string hash = "abc",
    string title = "",
    string genre = "",
    string artist = "",
    string comment1 = "",
    string comment2 = "",
    string comment3 = "")
  {
    return new MovieRecords
    {
      Movie_Name = name,
      Movie_Path = path,
      Tags = tags,
      Hash = hash,
      Title = title,
      Genre = genre,
      Artist = artist,
      Comment1 = comment1,
      Comment2 = comment2,
      Comment3 = comment3,
      Tag = string.IsNullOrEmpty(tags)
        ? []
        : [.. tags.Split('\n', StringSplitOptions.RemoveEmptyEntries)],
    };
  }

  [Fact]
  public void Build_TitlePartialMatch_FiltersRecords()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", title: "作品タイトルA"),
      CreateRecord("b.mp4", @"C:\b.mp4", title: "別作品"),
    };

    var result = MovieListFilter.Build(source, "タイトルA", "1");

    Assert.Single(result.Items);
    Assert.Equal("a.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_GenrePartialMatch_FiltersRecords()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", genre: "ジャンルX / ジャンルY"),
      CreateRecord("b.mp4", @"C:\b.mp4", genre: "その他"),
    };

    var result = MovieListFilter.Build(source, "ジャンルX", "1");

    Assert.Single(result.Items);
    Assert.Equal("a.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_ExactQuotedMatch_IncludesTitleAndGenre()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", title: "完全一致タイトル"),
      CreateRecord("b.mp4", @"C:\b.mp4", genre: "完全一致ジャンル"),
      CreateRecord("c.mp4", @"C:\c.mp4"),
    };

    var titleResult = MovieListFilter.Build(source, "\"完全一致タイトル\"", "1");
    Assert.Single(titleResult.Items);
    Assert.Equal("a.mp4", titleResult.Items[0].Movie_Name);

    var genreResult = MovieListFilter.Build(source, "\"完全一致ジャンル\"", "1");
    Assert.Single(genreResult.Items);
    Assert.Equal("b.mp4", genreResult.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_ArtistPartialMatch_FiltersRecords()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", artist: "メーカーA"),
      CreateRecord("b.mp4", @"C:\b.mp4", artist: "別メーカー"),
    };

    var result = MovieListFilter.Build(source, "メーカーA", "1");

    Assert.Single(result.Items);
    Assert.Equal("a.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_CommentPartialMatch_StillWorks()
  {
    var source = new[]
    {
      CreateRecord("a.mp4", @"C:\a.mp4", comment1: "メーカー名"),
      CreateRecord("b.mp4", @"C:\b.mp4"),
    };

    var result = MovieListFilter.Build(source, "メーカー", "1");

    Assert.Single(result.Items);
    Assert.Equal("a.mp4", result.Items[0].Movie_Name);
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
    string dbPath = Path.Combine(Path.GetTempPath(), $"imm-filter-{Guid.NewGuid():N}.wb");
    try
    {
      SQLite.CreateDatabase(dbPath);
      InsertMovieForFilterTest(dbPath, 1, "a.mp4", "");
      InsertMovieForFilterTest(dbPath, 2, "b.mp4", "x");

      var source = new[]
      {
        CreateRecord("a.mp4", @"C:\a.mp4", ""),
        CreateRecord("b.mp4", @"C:\b.mp4", "x"),
      };
      source[0].Movie_Id = 1;
      source[1].Movie_Id = 2;

      var context = new MovieListFilterContext { DbFullPath = dbPath };
      var result = MovieListFilter.Build(source, "{tag = ''}", "1", context);

      Assert.Single(result.Items);
      Assert.Equal("a.mp4", result.Items[0].Movie_Name);
    }
    finally
    {
      if (File.Exists(dbPath))
      {
        File.Delete(dbPath);
      }
    }
  }

  [Fact]
  public void Build_Tag_FiltersTaggedRecords()
  {
    string dbPath = Path.Combine(Path.GetTempPath(), $"imm-filter-{Guid.NewGuid():N}.wb");
    try
    {
      SQLite.CreateDatabase(dbPath);
      InsertMovieForFilterTest(dbPath, 1, "a.mp4", "");
      InsertMovieForFilterTest(dbPath, 2, "b.mp4", "x");
      InsertMovieForFilterTest(dbPath, 3, "c.mp4", "   ");

      var source = new[]
      {
        CreateRecord("a.mp4", @"C:\a.mp4", ""),
        CreateRecord("b.mp4", @"C:\b.mp4", "x"),
        CreateRecord("c.mp4", @"C:\c.mp4", "   "),
      };
      source[0].Movie_Id = 1;
      source[1].Movie_Id = 2;
      source[2].Movie_Id = 3;

      var context = new MovieListFilterContext { DbFullPath = dbPath };
      var result = MovieListFilter.Build(source, "{tag <> ''}", "1", context);

      Assert.Equal(2, result.Items.Count);
      Assert.Contains(result.Items, x => x.Movie_Name == "b.mp4");
      Assert.Contains(result.Items, x => x.Movie_Name == "c.mp4");
    }
    finally
    {
      if (File.Exists(dbPath))
      {
        File.Delete(dbPath);
      }
    }
  }

  private static void InsertMovieForFilterTest(string dbPath, long id, string name, string tag)
  {
    using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}");
    connection.Open();
    using var cmd = connection.CreateCommand();
    var now = DateTime.Now;
    cmd.CommandText =
      "insert into movie (movie_id, movie_name, movie_path, movie_length, movie_size, last_date, file_date, regist_date, hash, container, video, audio, extra, tag) " +
      "values (@id, @name, @path, 0, 0, @now, @now, @now, '', '', '', '', '', @tag)";
    cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@id", id));
    cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@name", name));
    cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@path", $@"C:\{name}"));
    cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@now", now));
    cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@tag", tag));
    cmd.ExecuteNonQuery();
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

      var result = MovieListFilter.Build(source, "{::nofile}", "1");

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

    var result = MovieListFilter.Build(source, "{::duplication}", "1");

    Assert.Equal(2, result.Items.Count);
    Assert.Equal(2, result.SearchCount);
  }

  [Fact]
  public void Build_NameDup_FiltersNormalizedDuplicateBodies()
  {
    var source = new[]
    {
      CreateRecord("abc-123.mp4", @"C:\abc-123.mp4", hash: "h1"),
      CreateRecord("abc-0123.mp4", @"C:\abc-0123.mp4", hash: "h2"),
      CreateRecord("abc-123x.mp4", @"C:\abc-123x.mp4", hash: "h3"),
      CreateRecord("unique-777.mp4", @"C:\unique-777.mp4", hash: "h4"),
    };

    var result = MovieListFilter.Build(source, "{::namedup}", "1");

    Assert.Equal(3, result.Items.Count);
    Assert.Equal(3, result.SearchCount);
    Assert.Equal("12", result.OverrideSortId);
  }

  [Fact]
  public void Build_NameDup_ExcludesHashDuplicates()
  {
    var source = new[]
    {
      CreateRecord("abc-123.mp4", @"C:\abc-123.mp4", hash: "samehash"),
      CreateRecord("abc-0123.mp4", @"C:\abc-0123.mp4", hash: "samehash"),
      CreateRecord("xyz-777.mp4", @"C:\xyz-777.mp4", hash: "u1"),
      CreateRecord("xyz-0777.mp4", @"C:\xyz-0777.mp4", hash: "u2"),
    };

    var result = MovieListFilter.Build(source, "{::namedup}", "1");

    Assert.Equal(2, result.Items.Count);
    Assert.All(result.Items, x => Assert.NotEqual("samehash", x.Hash));
  }

  [Fact]
  public void Build_NameDupExact_KeepsSeriesLettersSeparate()
  {
    var source = new[]
    {
      CreateRecord("EFGH-003A.wmv", @"C:\EFGH-003A.wmv", hash: "h1"),
      CreateRecord("EFGH-003B.wmv", @"C:\EFGH-003B.wmv", hash: "h2"),
      CreateRecord("abc-123.mp4", @"C:\abc-123.mp4", hash: "h3"),
      CreateRecord("abc-0123.mp4", @"C:\abc-0123.mp4", hash: "h4"),
    };

    var result = MovieListFilter.Build(source, "{::namedupexact}", "1");

    Assert.Equal(2, result.Items.Count);
    Assert.All(result.Items, x => Assert.Contains("abc-", x.Movie_Name, StringComparison.OrdinalIgnoreCase));
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
    string errorMoviePath = Path.Combine(Path.GetTempPath(), $"imm-error-{Guid.NewGuid():N}.mp4");
    string okMoviePath = Path.Combine(Path.GetTempPath(), $"imm-ok-{Guid.NewGuid():N}.mp4");
    string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-filter-err-{Guid.NewGuid():N}");
    Directory.CreateDirectory(thumbRoot);
    try
    {
      File.WriteAllText(errorMoviePath, "movie");
      File.WriteAllText(okMoviePath, "movie");

      var cache = new ThumbnailLayoutCache();
      cache.Refresh("testdb", thumbRoot);

      var errorRecord = CreateRecord("error.mp4", errorMoviePath, hash: "errhash");
      var okRecord = CreateRecord("ok.mp4", okMoviePath, hash: "okhash");
      var source = new[] { errorRecord, okRecord };

      var context = new MovieListFilterContext
      {
        CurrentSkinEngine = SkinEngine.Wpf,
        ThumbnailCache = cache,
      };

      var wpfLayout = new ThumbnailLayoutSpec(400, 225, 1, 1);
      string errorThumb = cache.GetExpectedThumbPath(
        wpfLayout,
        ThumbnailMovieNaming.GetMovieBody(errorRecord),
        errorRecord.Hash);
      string errorTemplate = cache.GetErrorPath(2);
      Directory.CreateDirectory(Path.GetDirectoryName(errorThumb)!);
      File.Copy(errorTemplate, errorThumb, true);

      string okThumb = cache.GetExpectedThumbPath(
        wpfLayout,
        ThumbnailMovieNaming.GetMovieBody(okRecord),
        okRecord.Hash);
      Directory.CreateDirectory(Path.GetDirectoryName(okThumb)!);
      byte[] composite = new byte[128];
      BitConverter.GetBytes((ushort)1).CopyTo(composite, composite.Length - 60);
      File.WriteAllBytes(okThumb, composite);

      var result = MovieListFilter.Build(source, "{::error}", "1", context);

      Assert.Single(result.Items);
      Assert.Equal("error.mp4", result.Items[0].Movie_Name);
    }
    finally
    {
      if (File.Exists(errorMoviePath))
      {
        File.Delete(errorMoviePath);
      }

      if (File.Exists(okMoviePath))
      {
        File.Delete(okMoviePath);
      }

      if (Directory.Exists(thumbRoot))
      {
        Directory.Delete(thumbRoot, true);
      }
    }
  }

  [Fact]
  public void Build_StarTag_MatchesExactTagOnly()
  {
    var source = new[]
    {
      CreateRecord("one.mp4", @"C:\one.mp4", "★"),
      CreateRecord("three.mp4", @"C:\three.mp4", "★★★"),
      CreateRecord("five.mp4", @"C:\five.mp4", "★★★★★"),
    };

    var result = MovieListFilter.Build(source, "★★★", "1");

    Assert.Single(result.Items);
    Assert.Equal("three.mp4", result.Items[0].Movie_Name);
  }

  [Fact]
  public void Build_BangPrefix_DoesTagOnlyExactSearch()
  {
    var source = new[]
    {
      CreateRecord("alpha.mp4", @"C:\alpha.mp4", "drama"),
      // ファイル名に drama を含むがタグには無いレコードはヒットしない。
      CreateRecord("drama-show.mp4", @"C:\drama-show.mp4", "comedy"),
    };

    var result = MovieListFilter.Build(source, "!drama", "1");

    Assert.Single(result.Items);
    Assert.Equal("alpha.mp4", result.Items[0].Movie_Name);
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
