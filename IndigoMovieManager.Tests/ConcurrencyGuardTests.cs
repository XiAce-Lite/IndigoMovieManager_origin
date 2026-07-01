using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
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
    private static readonly ThumbnailLayoutSpec ListLayout = new(160, 120, 1, 1);

    [Fact]
    public void BeginJob_cancels_previous_job_token()
    {
        var coordinator = new ThumbnailJobCoordinator();
        int firstJobId = coordinator.BeginJob(ListLayout.Key);
        CancellationToken firstToken = coordinator.GetJobCancellationToken(firstJobId);
        Assert.False(firstToken.IsCancellationRequested);

        coordinator.BeginJob("other-layout");

        Assert.True(firstToken.IsCancellationRequested);
    }

    [Fact]
    public void TryComplete_returns_final_counts_before_job_removal()
    {
        var coordinator = new ThumbnailJobCoordinator();
        int jobId = coordinator.BeginJob(ListLayout.Key);
        var item = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout };
        coordinator.RegisterWork(jobId, [item]);
        coordinator.MarkInFlight(item);

        ThumbnailJobCoordinator.Snapshot snapshot = coordinator.TryComplete(item);

        Assert.Equal(jobId, snapshot.JobId);
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Completed);
        Assert.True(snapshot.IsComplete);
        Assert.Equal(0, coordinator.GetSnapshot(jobId).Total);
    }

    [Fact]
    public void AbandonAndClearQueue_marks_previous_job_abandoned()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var firstJobItem = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };
        scheduler.EnqueueWork(firstJobItem, ListLayout.Key, beginNewJob: true);

        scheduler.AbandonAndClearQueue("other-layout");

        Assert.False(scheduler.JobCoordinator.ShouldProcess(firstJobItem));
    }

    [Fact]
    public void TryRegisterManualWork_allows_requeue_when_not_in_flight()
    {
        var coordinator = new ThumbnailJobCoordinator();
        var first = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout };
        Assert.True(coordinator.TryRegisterSilentWork(first));

        var manual = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout, IsManual = true };
        Assert.True(coordinator.TryRegisterManualWork(manual));
    }

    [Fact]
    public void CancelTrackedForMovie_removes_pending_work()
    {
        var coordinator = new ThumbnailJobCoordinator();
        var item = new QueueObj { MovieId = 99, ThumbnailLayout = ListLayout };
        coordinator.TryRegisterSilentWork(item);
        coordinator.CancelTrackedForMovie(99);
        Assert.False(coordinator.IsTracked(99, ListLayout.Key));
    }

    [Fact]
    public void ClearSilentQueue_preserves_visible_job_items()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var visible = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" };
        var silent = new QueueObj { MovieId = 2, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" };

        scheduler.EnqueueWork(visible, ListLayout.Key, beginNewJob: true);
        scheduler.EnqueueSilentWork(silent);

        Assert.Equal(2, scheduler.Queue.Count);

        scheduler.ClearSilentQueue();

        Assert.Single(scheduler.Queue);
        Assert.True(scheduler.JobCoordinator.ShouldProcess(visible));
        Assert.False(scheduler.JobCoordinator.IsTracked(2, ListLayout.Key));
    }

    [Fact]
    public async Task StartTabSwitchJobAsync_enqueues_visible_job()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-{Guid.NewGuid():N}");
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mod");
        Directory.CreateDirectory(thumbRoot);
        try
        {
            await File.WriteAllTextAsync(moviePath, "test");

            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            var records = new List<MovieRecords>
            {
                new()
                {
                    Movie_Id = 1,
                    Movie_Path = moviePath,
                    Movie_Name = "movie",
                    Hash = "abc123",
                },
            };

            int buildEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                buildEpoch);

            Assert.Single(scheduler.Queue);
            scheduler.Queue.TryDequeue(out QueueObj item);
            Assert.NotNull(item);
            Assert.NotEqual(ThumbnailJobCoordinator.SilentJobId, item.JobId);
            Assert.True(scheduler.JobCoordinator.ShouldProcess(item));
        }
        finally
        {
            if (File.Exists(moviePath))
            {
                File.Delete(moviePath);
            }

            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }

    [Fact]
    public async Task StartTabSwitchJobAsync_discards_build_superseded_by_abandon()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-{Guid.NewGuid():N}");
        string movieDir = Path.Combine(Path.GetTempPath(), $"imm-movies-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        Directory.CreateDirectory(movieDir);
        try
        {
            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            var largeScope = new List<MovieRecords>();
            for (int i = 0; i < 300; i++)
            {
                string moviePath = Path.Combine(movieDir, $"movie{i}.mod");
                await File.WriteAllTextAsync(moviePath, "test");
                largeScope.Add(new MovieRecords
                {
                    Movie_Id = i + 1,
                    Movie_Path = moviePath,
                    Movie_Name = $"movie{i}",
                    Hash = $"hash{i}",
                });
            }

            var smallScope = new List<MovieRecords> { largeScope[0] };

            scheduler.AbandonAndClearQueue(ListLayout.Key);
            int staleEpoch = scheduler.TabSwitchBuildGeneration;
            Task staleBuild = scheduler.StartTabSwitchJobAsync(
                ListLayout,
                largeScope,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                staleEpoch);
            scheduler.AbandonAndClearQueue(ListLayout.Key);
            await staleBuild.ConfigureAwait(true);

            int currentEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                smallScope,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                currentEpoch);

            Assert.Single(scheduler.Queue);
        }
        finally
        {
            if (Directory.Exists(movieDir))
            {
                Directory.Delete(movieDir, true);
            }

            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }

    [Fact]
    public void BuildTabSwitchWork_skips_record_when_physical_file_missing()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        try
        {
            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            var records = new List<MovieRecords>
            {
                new()
                {
                    Movie_Id = 1,
                    Movie_Path = Path.Combine(Path.GetTempPath(), $"imm-missing-{Guid.NewGuid():N}.mod"),
                    Movie_Name = "movie",
                    Hash = "abc123",
                },
            };

            List<QueueObj> work = scheduler.BuildTabSwitchWork(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1);

            Assert.Empty(work);
        }
        finally
        {
            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }
}
