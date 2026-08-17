using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WhiteBrowserBraceSearchTests
{
    private static MovieRecords CreateRecord(
        long id,
        string name,
        string path,
        string tags = "",
        string hash = "abc")
    {
        return new MovieRecords
        {
            Movie_Id = id,
            Movie_Name = name,
            Movie_Path = path,
            Tags = tags,
            Hash = hash,
            Tag = string.IsNullOrEmpty(tags)
                ? []
                : [.. tags.Split('\n', StringSplitOptions.RemoveEmptyEntries)],
        };
    }

    [Fact]
    public void TryApply_sql_tag_empty_filters_untagged_records()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-brace-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, 1, "a.mp4", "");
            InsertMovie(dbPath, 2, "b.mp4", "tagged");

            var source = new[]
            {
                CreateRecord(1, "a.mp4", @"C:\a.mp4", ""),
                CreateRecord(2, "b.mp4", @"C:\b.mp4", "tagged"),
            };

            var context = new MovieListFilterContext { DbFullPath = dbPath };
            bool applied = WhiteBrowserBraceSearch.TryApply(
                source,
                "tag = ''",
                context,
                out IReadOnlyList<MovieRecords> filtered,
                out _);

            Assert.True(applied);
            Assert.Single(filtered);
            Assert.Equal(1, filtered[0].Movie_Id);
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
    public void TryApply_special_duplication_forces_size_sort()
    {
        var source = new[]
        {
            CreateRecord(1, "a.mp4", @"C:\a.mp4", hash: "dup"),
            CreateRecord(2, "b.mp4", @"C:\b.mp4", hash: "dup"),
            CreateRecord(3, "c.mp4", @"C:\c.mp4", hash: "unique"),
        };

        bool applied = WhiteBrowserBraceSearch.TryApply(
            source,
            "::duplication",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> filtered,
            out string overrideSortId);

        Assert.True(applied);
        Assert.Equal(2, filtered.Count);
        Assert.Equal("16", overrideSortId);
    }

    [Fact]
    public void TryApply_special_namedup_matches_normalized_file_bodies()
    {
        var source = new[]
        {
            CreateRecord(1, "abc-123.mp4", @"C:\abc-123.mp4", hash: "h1"),
            CreateRecord(2, "abc-0123.mp4", @"C:\abc-0123.mp4", hash: "h2"),
            CreateRecord(3, "abc-123x.mp4", @"C:\abc-123x.mp4", hash: "h3"),
            CreateRecord(4, "xyz-999.mp4", @"C:\xyz-999.mp4", hash: "h4"),
        };

        bool applied = WhiteBrowserBraceSearch.TryApply(
            source,
            "::namedup",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> filtered,
            out string overrideSortId);

        Assert.True(applied);
        Assert.Equal(3, filtered.Count);
        Assert.Equal("12", overrideSortId);
    }

    [Fact]
    public void TryApply_special_namedup_excludes_hash_duplicate_group()
    {
        var source = new[]
        {
            CreateRecord(1, "abc-123.mp4", @"C:\abc-123.mp4", hash: "samehash"),
            CreateRecord(2, "abc-0123.mp4", @"C:\abc-0123.mp4", hash: "samehash"),
            CreateRecord(3, "xyz-777.mp4", @"C:\xyz-777.mp4", hash: "u1"),
            CreateRecord(4, "xyz-0777.mp4", @"C:\xyz-0777.mp4", hash: "u2"),
        };

        bool applied = WhiteBrowserBraceSearch.TryApply(
            source,
            "::namedup",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> filtered,
            out _);

        Assert.True(applied);
        Assert.Equal(2, filtered.Count);
        Assert.DoesNotContain(filtered, x => x.Hash == "samehash");
    }

    [Theory]
    [InlineData("abc-123", "abc-123")]
    [InlineData("abc-0123", "abc-123")]
    [InlineData("abc_123", "abc-123")]
    [InlineData("abc-123x", "abc-123")]
    [InlineData("zz-ppv-001234", "zz-ppv-1234")]
    [InlineData("efgh-003a", "efgh-3")]
    [InlineData("title-cd1", "title")]
    [InlineData("title-dvd2", "title")]
    [InlineData("title-u", "title")]
    [InlineData("title-uc", "title")]
    [InlineData("abc-123-u", "abc-123")]
    [InlineData("abc-123-uc", "abc-123")]
    [InlineData("xxdvd100", "xxdvd-100")]
    [InlineData("xxdvd200", "xxdvd-200")]
    [InlineData("xxdvd-100", "xxdvd-100")]
    public void NormalizeDuplicateNameKey_fuzzy_absorbs_common_variations(string body, string expected)
    {
        Assert.Equal(expected, WhiteBrowserBraceSearch.NormalizeDuplicateNameKey(body, exact: false));
    }

    [Fact]
    public void TryApply_special_namedup_does_not_collapse_maker_codes_containing_dvd()
    {
        var source = new[]
        {
            CreateRecord(1, "xxdvd100.avi", @"C:\xxdvd100.avi", hash: "h1"),
            CreateRecord(2, "xxdvd200.avi", @"C:\xxdvd200.avi", hash: "h2"),
            CreateRecord(3, "abc-123.mp4", @"C:\abc-123.mp4", hash: "h3"),
            CreateRecord(4, "abc-0123.mp4", @"C:\abc-0123.mp4", hash: "h4"),
        };

        bool applied = WhiteBrowserBraceSearch.TryApply(
            source,
            "::namedup",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> filtered,
            out _);

        Assert.True(applied);
        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, x => Assert.StartsWith("abc-", x.Movie_Name, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("abc-123", "abc-123")]
    [InlineData("abc-0123", "abc-123")]
    [InlineData("abc_123", "abc-123")]
    [InlineData("abc-123x", "abc-123x")]
    [InlineData("efgh-003a", "efgh-3a")]
    [InlineData("efgh-003b", "efgh-3b")]
    [InlineData("title-cd1", "title-cd1")]
    [InlineData("title-part2", "title-part2")]
    public void NormalizeDuplicateNameKey_exact_keeps_series_suffixes(string body, string expected)
    {
        Assert.Equal(expected, WhiteBrowserBraceSearch.NormalizeDuplicateNameKey(body, exact: true));
    }

    [Fact]
    public void TryApply_special_namedupexact_keeps_series_letters_separate()
    {
        var source = new[]
        {
            CreateRecord(1, "EFGH-003A.wmv", @"C:\EFGH-003A.wmv", hash: "h1"),
            CreateRecord(2, "EFGH-003B.wmv", @"C:\EFGH-003B.wmv", hash: "h2"),
            CreateRecord(3, "abc-123.mp4", @"C:\abc-123.mp4", hash: "h3"),
            CreateRecord(4, "abc-0123.mp4", @"C:\abc-0123.mp4", hash: "h4"),
        };

        bool appliedExact = WhiteBrowserBraceSearch.TryApply(
            source,
            "::namedupexact",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> exactFiltered,
            out string exactSortId);

        bool appliedFuzzy = WhiteBrowserBraceSearch.TryApply(
            source,
            "::namedup",
            new MovieListFilterContext(),
            out IReadOnlyList<MovieRecords> fuzzyFiltered,
            out _);

        Assert.True(appliedExact);
        Assert.True(appliedFuzzy);
        Assert.Equal(2, exactFiltered.Count);
        Assert.DoesNotContain(exactFiltered, x => x.Movie_Name.StartsWith("EFGH", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, fuzzyFiltered.Count);
        Assert.Equal("12", exactSortId);
    }

    [Fact]
    public void Build_sql_where_tag_empty_filters_untagged_records()
    {
        var source = new[]
        {
            CreateRecord(1, "a.mp4", @"C:\a.mp4", ""),
            CreateRecord(2, "b.mp4", @"C:\b.mp4", "tagged"),
        };

        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-brace-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, 1, "a.mp4", "");
            InsertMovie(dbPath, 2, "b.mp4", "tagged");

            var context = new MovieListFilterContext { DbFullPath = dbPath };
            var result = MovieListFilter.Build(source, "{tag = ''}", "1", context);

            Assert.Single(result.Items);
            Assert.Equal(1, result.Items[0].Movie_Id);
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
    public void TryApply_legacy_notag_alias_is_abolished()
    {
        var source = new[]
        {
            CreateRecord(1, "a.mp4", @"C:\a.mp4", ""),
            CreateRecord(2, "b.mp4", @"C:\b.mp4", "tagged"),
        };

        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-brace-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, 1, "a.mp4", "");
            InsertMovie(dbPath, 2, "b.mp4", "tagged");

            var context = new MovieListFilterContext { DbFullPath = dbPath };

            // "notag" は列名として存在しないため SQL エラー → 0 件（廃止済み）。
            bool applied = WhiteBrowserBraceSearch.TryApply(
                source,
                "notag",
                context,
                out IReadOnlyList<MovieRecords> filtered,
                out _);

            Assert.True(applied);
            Assert.Empty(filtered);
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
    public void TryApply_where_clause_with_create_time_column_is_allowed()
    {
        // create_time 列は禁止語 CREATE を含むが、単語境界判定で誤検知しないこと。
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-brace-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, 1, "a.mp4", "");

            var source = new[] { CreateRecord(1, "a.mp4", @"C:\a.mp4", "") };
            var context = new MovieListFilterContext { DbFullPath = dbPath };

            bool applied = WhiteBrowserBraceSearch.TryApply(
                source,
                "create_time <> ''",
                context,
                out IReadOnlyList<MovieRecords> filtered,
                out _);

            Assert.True(applied);
            Assert.NotNull(filtered);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static void InsertMovie(string dbPath, long id, string name, string tag)
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
}
