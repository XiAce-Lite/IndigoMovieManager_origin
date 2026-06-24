using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MediaExtensionSettingsTests
{
    [Theory]
    [InlineData(@"C:\video\sample.mod", true)]
    [InlineData(@"C:\video\sample.MOD", true)]
    [InlineData(@"C:\video\sample.mp4", true)]
    [InlineData(@"C:\video\sample.txt", false)]
    public void MatchesExtension_respects_configured_patterns(string path, bool expected)
    {
        const string checkExt = "*.mp4,*.mod";
        Assert.Equal(expected, MediaExtensionSettings.MatchesExtension(path, checkExt));
    }

    [Fact]
    public void EnsureRequiredExtensions_adds_mod_when_missing()
    {
        Properties.Settings.Default.CheckExt = "*.mp4";
        MediaExtensionSettings.EnsureRequiredExtensions();
        Assert.Contains("*.mod", Properties.Settings.Default.CheckExt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("*.zip", Properties.Settings.Default.CheckExt, StringComparison.OrdinalIgnoreCase);
    }
}

public class MovieInfoFallbackTests
{
    [Fact]
    public void TryCreateMovieInfo_succeeds_for_mod_without_opencv_probe()
    {
        string path = Path.Combine(Path.GetTempPath(), $"imm-test-{Guid.NewGuid():N}.mod");
        File.WriteAllBytes(path, new byte[1024]);
        try
        {
            Assert.True(MovieRegistrationHelper.TryCreateMovieInfo(path, out MovieInfo info));
            Assert.NotNull(info);
            Assert.Equal(path, info.MoviePath);
            Assert.True(info.MovieSize > 0);
            Assert.False(string.IsNullOrWhiteSpace(info.Hash));
            Assert.Equal(0, info.MovieLength);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class FolderCheckServicePathTests
{
    [Fact]
    public void IsFileRegistered_uses_case_insensitive_path_compare()
    {
        var records = new[]
        {
            new MovieRecords { Movie_Path = @"I:\Secret\MOV003.MOD" },
        };

        Assert.True(FolderCheckService.IsFileRegistered(records, @"i:\secret\mov003.mod"));
    }
}

public class FolderCheckRegistrationTests
{
    [Fact]
    public async Task ShouldRegisterDiscoveredFile_uses_database_not_memory()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-reg-{Guid.NewGuid():N}.wb");
        string mediaPath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(mediaPath, [0x50, 0x4B, 0x05, 0x06]);
        try
        {
            SQLite.CreateDatabase(dbPath);
            Assert.True(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));

            MovieInfo registered = await MovieRegistrationHelper.TryRegisterDiscoveredFileAsync(dbPath, mediaPath);
            Assert.NotNull(registered);
            Assert.False(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));

            MovieRecords[] staleRecords = [new MovieRecords { Movie_Path = mediaPath }];
            Assert.True(FolderCheckService.IsFileRegistered(staleRecords, mediaPath));
            Assert.False(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));
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
    public async Task TryRegisterDiscoveredFileAsync_revives_stale_movie_name_row()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-revive-{Guid.NewGuid():N}.wb");
        string mediaPath = Path.Combine(Path.GetTempPath(), $"imm-revive-{Guid.NewGuid():N}.mod");
        File.WriteAllBytes(mediaPath, new byte[1024]);
        try
        {
            SQLite.CreateDatabase(dbPath);
            MovieInfo first = await MovieRegistrationHelper.TryRegisterDiscoveredFileAsync(dbPath, mediaPath);
            Assert.NotNull(first);

            SQLite.DeleteMovieTable(dbPath, first.MovieId);
            Assert.True(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));

            using (var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO movie (movie_id, movie_name, movie_path, movie_length, movie_size, last_date, file_date, regist_date, hash, container, video, audio, extra) " +
                    "VALUES (99, @movie_name, @movie_path, 0, 1, @now, @now, @now, @hash, '', '', '', '')";
                cmd.Parameters.AddWithValue("@movie_name", Path.GetFileNameWithoutExtension(mediaPath).ToLowerInvariant());
                cmd.Parameters.AddWithValue("@movie_path", Path.Combine(Path.GetTempPath(), "missing-file.mod"));
                cmd.Parameters.AddWithValue("@hash", first.Hash);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.ExecuteNonQuery();
            }

            Assert.True(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));
            MovieInfo revived = await MovieRegistrationHelper.TryRegisterDiscoveredFileAsync(dbPath, mediaPath);
            Assert.NotNull(revived);
            Assert.Equal(mediaPath, revived.MoviePath);
            Assert.False(FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, mediaPath));
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
}

public class ZipImageViewerLauncherTests
{
    [Fact]
    public void ResolveViewer_prefers_database_settings()
    {
        (string program, string param) = ZipImageViewerLauncher.ResolveViewer(
            @"C:\Tools\viewer.exe",
            "<file>",
            @"C:\Default\viewer.exe",
            "\"<file>\"");

        Assert.Equal(@"C:\Tools\viewer.exe", program);
        Assert.Equal("<file>", param);
    }

    [Fact]
    public void ResolveViewer_falls_back_to_common_zip_viewer()
    {
        (string program, string param) = ZipImageViewerLauncher.ResolveViewer(
            "",
            "",
            @"C:\Default\viewer.exe",
            "\"<file>\"");

        Assert.Equal(@"C:\Default\viewer.exe", program);
        Assert.Equal("\"<file>\"", param);
    }
}
