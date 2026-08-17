using IndigoMovieManager;
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
    public void Batch_enqueue_with_beginNewJob_sets_total_to_batch_size()
    {
        var scheduler = new ThumbnailQueueScheduler();
        List<QueueObj> batch =
        [
            new() { MovieId = 1, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" },
            new() { MovieId = 2, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" },
            new() { MovieId = 3, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" },
        ];

        scheduler.EnqueueWork(batch, ListLayout.Key, beginNewJob: true);

        ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot();
        Assert.Equal(3, snapshot.Total);
        Assert.Equal(3, scheduler.Queue.Count);
    }

    [Fact]
    public void Sequential_enqueue_without_beginNewJob_accumulates_total()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var first = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };
        var second = new QueueObj
        {
            MovieId = 2,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };

        scheduler.EnqueueWork(first, ListLayout.Key, beginNewJob: true);
        scheduler.EnqueueWork(second, ListLayout.Key, beginNewJob: false);

        ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot();
        Assert.Equal(2, snapshot.Total);
        Assert.True(scheduler.JobCoordinator.ShouldProcess(first));
        Assert.True(scheduler.JobCoordinator.ShouldProcess(second));
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
    public void ClearSilentQueue_and_Abandon_preserve_retained_pregen_items()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var otherLayout = new ThumbnailLayoutSpec(200, 150, 2, 2);
        var visible = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout, DbFullPath = @"C:\a\db.sqlite" };
        var retained = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = otherLayout,
            DbFullPath = @"C:\a\db.sqlite",
            RetainAcrossLayoutSwitch = true,
        };
        var plainSilent = new QueueObj
        {
            MovieId = 2,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };

        scheduler.EnqueueWork(visible, ListLayout.Key, beginNewJob: true);
        scheduler.EnqueueSilentWork(retained);
        scheduler.EnqueueSilentWork(plainSilent);

        scheduler.ClearSilentQueue();
        Assert.Equal(2, scheduler.Queue.Count);
        Assert.Contains(scheduler.Queue, q => q.RetainAcrossLayoutSwitch);
        Assert.DoesNotContain(scheduler.Queue, q => q.MovieId == 2);

        scheduler.AbandonAndClearQueue(otherLayout.Key, preserveRetainedWork: true);
        Assert.Single(scheduler.Queue);
        Assert.True(scheduler.Queue.TryPeek(out QueueObj kept));
        Assert.True(kept.RetainAcrossLayoutSwitch);
        Assert.True(scheduler.JobCoordinator.ShouldProcess(kept));

        scheduler.AbandonAndClearQueue(ListLayout.Key, preserveRetainedWork: false);
        Assert.Empty(scheduler.Queue);
    }

    [Fact]
    public async Task StartTabSwitchJobAsync_enqueues_all_pending_items()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-multi-{Guid.NewGuid():N}");
        string movieDir = Path.Combine(Path.GetTempPath(), $"imm-movies-multi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        Directory.CreateDirectory(movieDir);
        try
        {
            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            var records = new List<MovieRecords>();
            for (int i = 0; i < 5; i++)
            {
                string moviePath = Path.Combine(movieDir, $"movie{i}.mod");
                await File.WriteAllTextAsync(moviePath, "test");
                records.Add(new MovieRecords
                {
                    Movie_Id = i + 1,
                    Movie_Path = moviePath,
                    Movie_Name = $"movie{i}",
                    Hash = $"hash{i}",
                });
            }

            int buildEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                buildEpoch);

            Assert.Equal(5, scheduler.Queue.Count);
            ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot();
            Assert.Equal(5, snapshot.Total);

            int processable = 0;
            while (scheduler.Queue.TryDequeue(out QueueObj item))
            {
                if (scheduler.JobCoordinator.ShouldProcess(item))
                {
                    processable++;
                }
            }

            Assert.Equal(5, processable);
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
    public void IsAcceptingWork_returns_false_after_job_completes()
    {
        var coordinator = new ThumbnailJobCoordinator();
        int jobId = coordinator.BeginJob(ListLayout.Key);
        var item = new QueueObj { MovieId = 1, ThumbnailLayout = ListLayout };
        coordinator.RegisterWork(jobId, [item]);
        coordinator.MarkInFlight(item);
        coordinator.TryComplete(item);

        Assert.False(coordinator.IsAcceptingWork(jobId));
    }

    [Fact]
    public void EnqueueWork_after_completed_job_starts_fresh_job()
    {
        var scheduler = new ThumbnailQueueScheduler();
        var first = new QueueObj
        {
            MovieId = 1,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };
        var second = new QueueObj
        {
            MovieId = 2,
            ThumbnailLayout = ListLayout,
            DbFullPath = @"C:\a\db.sqlite",
        };

        scheduler.EnqueueWork(first, ListLayout.Key, beginNewJob: true);
        int firstJobId = scheduler.JobCoordinator.CurrentJobId;
        scheduler.JobCoordinator.MarkInFlight(first);
        scheduler.JobCoordinator.TryComplete(first);

        scheduler.EnqueueWork(second, ListLayout.Key, beginNewJob: false);

        int secondJobId = scheduler.JobCoordinator.CurrentJobId;
        Assert.NotEqual(firstJobId, secondJobId);
        Assert.True(scheduler.JobCoordinator.ShouldProcess(second));
        ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot(secondJobId);
        Assert.Equal(1, snapshot.Total);
    }

    [Fact]
    public async Task StartTabSwitchJobAsync_skips_when_composite_thumbnail_exists()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-skip-{Guid.NewGuid():N}");
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-skip-{Guid.NewGuid():N}.mp4");
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
                    Movie_Name = $"{Path.GetFileName(moviePath)}.mp4",
                    Hash = "abc123",
                },
            };

            string body = ThumbnailMovieNaming.GetMovieBody(records[0]);
            string thumbPath = cache.GetExpectedThumbPath(ListLayout, body, records[0].Hash);
            WriteCompositeThumb(thumbPath, ListLayout.Width, ListLayout.Height);

            int buildEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                buildEpoch).ConfigureAwait(true);

            Assert.Empty(scheduler.Queue);
            Assert.Equal(0, scheduler.JobCoordinator.GetSnapshot().Total);
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
    public async Task StartTabSwitchJobAsync_appends_without_abandoning_discovered_job()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-append-{Guid.NewGuid():N}");
        string movieDir = Path.Combine(Path.GetTempPath(), $"imm-movies-append-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        Directory.CreateDirectory(movieDir);
        try
        {
            var cache = new ThumbnailLayoutCache();
            cache.Refresh("testdb", thumbRoot);

            string discoveredPath = Path.Combine(movieDir, "discovered.mp4");
            await File.WriteAllTextAsync(discoveredPath, "new");
            var discovered = new QueueObj
            {
                MovieId = 99,
                MovieFullPath = discoveredPath,
                ThumbnailLayout = ListLayout,
                DbFullPath = @"C:\fake\db.wb",
            };
            scheduler.EnqueueWork(discovered, ListLayout.Key, beginNewJob: true);
            int discoveredJobId = scheduler.JobCoordinator.CurrentJobId;

            string missingPath = Path.Combine(movieDir, "missing.mp4");
            await File.WriteAllTextAsync(missingPath, "old");
            var records = new List<MovieRecords>
            {
                new()
                {
                    Movie_Id = 1,
                    Movie_Path = missingPath,
                    Movie_Name = "missing",
                    Hash = "hash1",
                },
            };

            int buildEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                buildEpoch).ConfigureAwait(true);

            Assert.Equal(discoveredJobId, scheduler.JobCoordinator.CurrentJobId);
            Assert.False(scheduler.ShouldBeginNewVisibleJob(ListLayout.Key));

            var processableIds = new HashSet<long>();
            while (scheduler.Queue.TryDequeue(out QueueObj item))
            {
                if (scheduler.JobCoordinator.ShouldProcess(item))
                {
                    processableIds.Add(item.MovieId);
                }
            }

            Assert.Contains(99L, processableIds);
            Assert.Contains(1L, processableIds);
            Assert.Equal(2, scheduler.JobCoordinator.GetSnapshot(discoveredJobId).Total);
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
    public void ShouldBeginNewVisibleJob_is_false_while_job_has_pending_work()
    {
        var scheduler = new ThumbnailQueueScheduler();
        scheduler.EnqueueWork(
            new QueueObj
            {
                MovieId = 1,
                ThumbnailLayout = ListLayout,
                DbFullPath = @"C:\fake\db.wb",
            },
            ListLayout.Key,
            beginNewJob: true);

        Assert.False(scheduler.ShouldBeginNewVisibleJob(ListLayout.Key));
        Assert.True(scheduler.ShouldBeginNewVisibleJob("other-layout"));
    }

    [Fact]
    public async Task StartTabSwitchJobAsync_skips_when_thumbnail_file_exists_even_if_not_composite()
    {
        var scheduler = new ThumbnailQueueScheduler();
        string thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-tab-exists-{Guid.NewGuid():N}");
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-exists-{Guid.NewGuid():N}.mp4");
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

            string body = ThumbnailMovieNaming.GetMovieBody(records[0]);
            string thumbPath = cache.GetExpectedThumbPath(ListLayout, body, records[0].Hash);
            Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);
            await File.WriteAllTextAsync(thumbPath, "not-a-composite");

            int buildEpoch = scheduler.TabSwitchBuildGeneration;
            await scheduler.StartTabSwitchJobAsync(
                ListLayout,
                records,
                cache,
                @"C:\fake\db.wb",
                workGeneration: 1,
                buildEpoch).ConfigureAwait(true);

            Assert.Empty(scheduler.Queue);
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

    private static void WriteCompositeThumb(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new System.Drawing.Bitmap(width, height);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);

        var thumbInfo = new Tools.ThumbInfo
        {
            ThumbWidth = width,
            ThumbHeight = height,
            ThumbRows = 1,
            ThumbColumns = 1,
            ThumbCounts = 1,
        };
        thumbInfo.Add(0);
        thumbInfo.NewThumbInfo();
        ThumbnailMetadataWriter.AppendMetadata(path, thumbInfo);
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
