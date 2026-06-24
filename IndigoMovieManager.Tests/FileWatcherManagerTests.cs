using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class FileWatcherManagerTests
{
    [Fact]
    public void Clear_disposes_watchers_and_invalidates_previous_session()
    {
        var manager = new FileWatcherManager();
        int sessionBefore = manager.CurrentSessionId;

        manager.AddWatcher(
            Path.GetTempPath(),
            sub: false,
            (_, _) => { },
            (_, _) => { });

        Assert.Single(manager.Watchers);

        manager.Clear();

        Assert.Empty(manager.Watchers);
        Assert.NotEqual(sessionBefore, manager.CurrentSessionId);
        Assert.False(manager.IsSessionActive(sessionBefore));
        Assert.True(manager.IsSessionActive(manager.CurrentSessionId));
    }
}

public class MainWindowSessionStateFolderCheckTests
{
    [Fact]
    public void SetActiveDb_bumps_folder_check_generation()
    {
        var state = new MainWindowSessionState();
        int before = state.FolderCheckGeneration;
        state.SetActiveDb(@"C:\test\db.sqlite");
        Assert.Equal(before + 1, state.FolderCheckGeneration);
    }
}
