using System.Data.SQLite;
using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MovieFileInfoHelperTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1023, 0)]
    [InlineData(1024, 1)]
    [InlineData(4096, 4)]
    public void ToMovieSizeKb_matches_registration_integer_division(long bytes, long expectedKb)
    {
        Assert.Equal(expectedKb, MovieFileInfoHelper.ToMovieSizeKb(bytes));
    }

    [Fact]
    public void TryGetMovieSizeKb_reads_file_length_as_kb()
    {
        string path = Path.Combine(Path.GetTempPath(), $"imm-size-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, new byte[4096]);
        try
        {
            Assert.True(MovieFileInfoHelper.TryGetMovieSizeKb(path, out long sizeKb));
            Assert.Equal(4, sizeKb);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetMovieSizeKb_returns_false_when_missing()
    {
        string path = Path.Combine(Path.GetTempPath(), $"imm-missing-{Guid.NewGuid():N}.mp4");
        Assert.False(MovieFileInfoHelper.TryGetMovieSizeKb(path, out long sizeKb));
        Assert.Equal(0, sizeKb);
    }

    [Fact]
    public void ApplyMovieSizeToRecord_sets_movie_size()
    {
        var rec = new MovieRecords { Movie_Size = 0 };
        MovieFileInfoHelper.ApplyMovieSizeToRecord(rec, 12);
        Assert.Equal(12, rec.Movie_Size);
    }
}

public class FileInfoRefreshServiceTests
{
    [Fact]
    public void RefreshCore_fills_zero_movie_size_from_disk_without_sinku()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-size-db-{Guid.NewGuid():N}.wb");
        string mediaPath = Path.Combine(Path.GetTempPath(), $"imm-size-movie-{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(mediaPath, new byte[8192]);
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, mediaPath, movieSizeKb: 0);

            var rec = new MovieRecords
            {
                Movie_Id = 1,
                Movie_Path = mediaPath,
                Movie_Size = 0,
            };

            FileInfoRefreshService.RefreshCore(dbPath, rec, action => action());

            Assert.Equal(8, rec.Movie_Size);
            Assert.Equal(8, ReadMovieSize(dbPath, 1));
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            if (File.Exists(mediaPath))
            {
                File.Delete(mediaPath);
            }
        }
    }

    [Fact]
    public void RefreshCore_fills_zero_zip_size_from_disk()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-zip-size-db-{Guid.NewGuid():N}.wb");
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-zip-size-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(zipPath, new byte[2048]);
        try
        {
            SQLite.CreateDatabase(dbPath);
            InsertMovie(dbPath, zipPath, movieSizeKb: 0);

            var rec = new MovieRecords
            {
                Movie_Id = 1,
                Movie_Path = zipPath,
                Movie_Size = 0,
                Container = "zip",
            };

            FileInfoRefreshService.RefreshCore(dbPath, rec, action => action());

            long expectedKb = MovieFileInfoHelper.ToMovieSizeKb(new FileInfo(zipPath).Length);
            Assert.Equal(expectedKb, rec.Movie_Size);
            Assert.Equal(expectedKb, ReadMovieSize(dbPath, 1));
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    private static void InsertMovie(string dbPath, string mediaPath, long movieSizeKb)
    {
        using SQLiteConnection connection = new($"Data Source={dbPath}");
        connection.Open();
        using SQLiteCommand cmd = connection.CreateCommand();
        DateTime now = DateTime.Now;
        cmd.CommandText =
            "INSERT INTO movie (movie_id, movie_name, movie_path, movie_length, movie_size, last_date, file_date, regist_date, hash, container, video, audio, extra) " +
            "VALUES (1, @name, @path, 0, @size, @now, @now, @now, '', '', '', '', '')";
        cmd.Parameters.AddWithValue("@name", Path.GetFileNameWithoutExtension(mediaPath).ToLowerInvariant());
        cmd.Parameters.AddWithValue("@path", mediaPath);
        cmd.Parameters.AddWithValue("@size", movieSizeKb);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();
    }

    private static long ReadMovieSize(string dbPath, long movieId)
    {
        using SQLiteConnection connection = new($"Data Source={dbPath}");
        connection.Open();
        using SQLiteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT movie_size FROM movie WHERE movie_id = @id";
        cmd.Parameters.AddWithValue("@id", movieId);
        return (long)cmd.ExecuteScalar();
    }
}
