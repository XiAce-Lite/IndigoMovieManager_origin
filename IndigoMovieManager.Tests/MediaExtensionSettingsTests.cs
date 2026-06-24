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
