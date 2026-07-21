using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WatchFolderDmmAutoServiceTests
{
    [Theory]
    [InlineData(@"C:\Watch\movie.mp4", @"C:\Watch", true, true)]
    [InlineData(@"C:\Watch\sub\movie.mp4", @"C:\Watch", true, true)]
    [InlineData(@"C:\Watch\sub\movie.mp4", @"C:\Watch", false, false)]
    [InlineData(@"C:\Watch\movie.mp4", @"C:\Watch", false, true)]
    [InlineData(@"D:\Other\movie.mp4", @"C:\Watch", true, false)]
    public void IsFileUnderWatchFolder_matches_expected_paths(
        string filePath,
        string watchDir,
        bool includeSubfolders,
        bool expected)
    {
        bool actual = WatchFolderDmmAutoService.IsFileUnderWatchFolder(
            filePath.Replace('\\', Path.DirectorySeparatorChar),
            watchDir.Replace('\\', Path.DirectorySeparatorChar),
            includeSubfolders);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void IsEnabledForMediaPath_returns_false_when_no_dmm_auto_rows()
    {
        string dbPath = CreateTempDb();
        try
        {
            InsertWatchRow(dbPath, @"C:\Watch", dmmAuto: false);
            Assert.False(WatchFolderDmmAutoService.IsEnabledForMediaPath(
                dbPath,
                @"C:\Watch\sample.mp4"));
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    [Fact]
    public void IsEnabledForMediaPath_returns_true_for_matching_folder()
    {
        string dbPath = CreateTempDb();
        try
        {
            InsertWatchRow(dbPath, @"C:\DmmAuto", dmmAuto: true, sub: true);
            InsertWatchRow(dbPath, @"C:\Other", dmmAuto: false, sub: true);

            Assert.True(WatchFolderDmmAutoService.IsEnabledForMediaPath(
                dbPath,
                @"C:\DmmAuto\sub\sample.mp4"));
            Assert.False(WatchFolderDmmAutoService.IsEnabledForMediaPath(
                dbPath,
                @"C:\Other\sample.mp4"));
        }
        finally
        {
            File.Delete(dbPath);
        }
    }

    private static string CreateTempDb()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-watch-dmm-{Guid.NewGuid():N}.wb");
        SQLite.CreateDatabase(dbPath);
        WatchFolderDmmAutoService.EnsureSchema(dbPath);
        return dbPath;
    }

    private static void InsertWatchRow(
        string dbPath,
        string dir,
        bool dmmAuto,
        bool sub = true,
        bool watch = true,
        bool auto = false)
    {
        SQLite.InsertWatchTable(dbPath, new WatchRecords
        {
            Dir = dir,
            DmmAuto = dmmAuto,
            Sub = sub,
            Watch = watch,
            Auto = auto,
        });
    }
}
