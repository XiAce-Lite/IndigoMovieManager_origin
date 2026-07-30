using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class FolderCheckScanHelperTests
{
    [Fact]
    public void Progress_messages_match_previous_mainwindow_format()
    {
        Assert.Equal(@"D:\watch 監視実施中…", FolderCheckService.FormatScanningMessage(@"D:\watch"));
        Assert.Equal(@"D:\watch に更新あり。", FolderCheckService.FormatHasUpdatesMessage(@"D:\watch"));
        Assert.Equal(@"D:\watch 監視完了", FolderCheckService.FormatCompletedMessage(@"D:\watch"));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void ShouldApplyResults_matches_previous_gate(
        bool registeredAny,
        bool foundUnregistered,
        bool expected)
    {
        Assert.Equal(
            expected,
            FolderCheckService.ShouldApplyResults(registeredAny, foundUnregistered));
    }

    [Fact]
    public void GetWatchSql_covers_modes()
    {
        Assert.Equal("SELECT * FROM watch where auto = 1", FolderCheckService.GetWatchSql(FolderCheckMode.Auto));
        Assert.Equal("SELECT * FROM watch where watch = 1", FolderCheckService.GetWatchSql(FolderCheckMode.Watch));
        Assert.Equal("SELECT * FROM watch", FolderCheckService.GetWatchSql(FolderCheckMode.Manual));
    }

    [Fact]
    public async Task ScanAndRegisterAsync_stops_when_inactive_before_work()
    {
        FolderCheckScanResult result = await FolderCheckService.ScanAndRegisterAsync(
            @"C:\missing.wb",
            [("C:\\missing-folder", false)],
            excludeExt: "",
            pathIndex: MoviePathRegistrationIndex.Load(@"C:\missing.wb"),
            callbacks: new FolderCheckScanCallbacks
            {
                IsStillActive = () => false,
            });

        Assert.False(result.RegisteredAny);
        Assert.False(result.FoundUnregistered);
        Assert.Empty(result.AddedThumbnailWork);
    }
}
