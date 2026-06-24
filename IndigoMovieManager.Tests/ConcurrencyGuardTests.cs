using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MainWindowSessionStateTests
{
    [Fact]
    public void SetActiveDb_bumps_filter_generation()
    {
        var state = new MainWindowSessionState();
        int before = state.FilterGeneration;
        state.SetActiveDb(@"C:\test\db.sqlite");
        Assert.Equal(before + 1, state.FilterGeneration);
        Assert.True(state.IsActiveDb(@"C:\test\db.sqlite"));
    }
}

public class ThumbnailJobCoordinatorTests
{
    [Fact]
    public void AbandonAndClearQueue_marks_previous_job_abandoned()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var firstJobItem = new QueueObj { MovieId = 1, Tabindex = 0, DbFullPath = @"C:\a\db.sqlite" };
        scheduler.EnqueueWork(firstJobItem, 0, beginNewJob: true);

        scheduler.AbandonAndClearQueue(1);

        Assert.False(scheduler.JobCoordinator.ShouldProcess(firstJobItem));
    }

    [Fact]
    public void TryRegisterManualWork_allows_requeue_when_not_in_flight()
    {
        var coordinator = new ThumbnailJobCoordinator();
        var first = new QueueObj { MovieId = 1, Tabindex = 0 };
        Assert.True(coordinator.TryRegisterSilentWork(first));

        var manual = new QueueObj { MovieId = 1, Tabindex = 0, IsManual = true };
        Assert.True(coordinator.TryRegisterManualWork(manual));
    }

    [Fact]
    public void CancelTrackedForMovie_removes_pending_work()
    {
        var coordinator = new ThumbnailJobCoordinator();
        var item = new QueueObj { MovieId = 99, Tabindex = 2 };
        coordinator.TryRegisterSilentWork(item);
        coordinator.CancelTrackedForMovie(99);
        Assert.False(coordinator.IsTracked(99, 2));
    }
}
