using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MoviePathRegistrationIndexTests
{
    [Fact]
    public async Task Load_and_IsRegistered_reflect_database_paths()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-index-{Guid.NewGuid():N}.wb");
        string mediaPath = Path.Combine(Path.GetTempPath(), $"imm-index-movie-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(mediaPath, [0x50, 0x4B, 0x05, 0x06]);
        try
        {
            SQLite.CreateDatabase(dbPath);
            MovieInfo registered = await MovieRegistrationHelper.TryRegisterDiscoveredFileAsync(dbPath, mediaPath);
            Assert.NotNull(registered);

            MoviePathRegistrationIndex index = MoviePathRegistrationIndex.Load(dbPath);
            Assert.True(index.IsRegistered(mediaPath));
            Assert.False(index.IsRegistered(Path.Combine(Path.GetTempPath(), "missing.zip")));
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
    public void FindUnregisteredFiles_skips_registered_paths()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"imm-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string registered = Path.Combine(folder, "known.zip");
        string fresh = Path.Combine(folder, "new.zip");
        string excluded = Path.Combine(folder, "skip.zip");
        File.WriteAllBytes(registered, [0x50, 0x4B, 0x05, 0x06]);
        File.WriteAllBytes(fresh, [0x50, 0x4B, 0x05, 0x06]);
        File.WriteAllBytes(excluded, [0x50, 0x4B, 0x05, 0x06]);
        string previousCheckExt = Properties.Settings.Default.CheckExt;
        try
        {
            Properties.Settings.Default.CheckExt = ".zip";
            string dbPath = Path.Combine(Path.GetTempPath(), $"imm-scan-db-{Guid.NewGuid():N}.wb");
            SQLite.CreateDatabase(dbPath);
            MoviePathRegistrationIndex index = MoviePathRegistrationIndex.Load(dbPath);
            index.Register(registered);

            List<string> found = MoviePathRegistrationIndex.FindUnregisteredFiles(
                index,
                folder,
                recurseSubdirectories: false,
                excludeExtSetting: ".zip");
            Assert.DoesNotContain(excluded, found);
            Assert.DoesNotContain(registered, found);

            List<string> foundWithoutExclude = MoviePathRegistrationIndex.FindUnregisteredFiles(
                index,
                folder,
                recurseSubdirectories: false);
            Assert.Equal(2, foundWithoutExclude.Count);
            Assert.Contains(fresh, foundWithoutExclude);
            Assert.Contains(excluded, foundWithoutExclude);

            File.Delete(dbPath);
        }
        finally
        {
            Properties.Settings.Default.CheckExt = previousCheckExt;
            Directory.Delete(folder, recursive: true);
        }
    }
}

public class ThumbnailValidityHelperFastPathTests
{
    [Fact]
    public void IsUsableCompositeThumbnail_returns_false_when_file_missing_without_opening()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"imm-missing-thumb-{Guid.NewGuid():N}.jpg");
        Assert.False(ThumbnailValidityHelper.IsUsableCompositeThumbnail(missing));
    }
}
