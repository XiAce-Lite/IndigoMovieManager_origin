using System.Data.SQLite;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

/// <summary>
/// F:\Temp の実動画を使い、極小レイアウトと大きめ 2×2 レイアウトで
/// スキン切替サムネ投入が全件処理されることをサンプルレイアウトで検証する。
/// </summary>
[CollectionDefinition("FTempSkinSwitchThumbnails", DisableParallelization = true)]
public sealed class FTempSkinSwitchThumbnailCollection;

[Collection("FTempSkinSwitchThumbnails")]
public class FTempSkinSwitchThumbnailTests
{
    private const string TempRoot = @"F:\Temp";
    private const string WbPath = @"F:\Temp\imm-tab-switch-sample.wb";
    private const string ThumbFolder = @"F:\Temp\imm-tab-switch-thumbs";
    private const string DbName = "immTabSwitchSample";

    public static readonly TheoryData<string, ThumbnailLayoutSpec> SampleLayouts = new()
    {
        { "TinySample", new ThumbnailLayoutSpec(80, 45, 2, 1) },
        { "LargeSample", new ThumbnailLayoutSpec(320, 180, 2, 2) },
    };

    [Theory]
    [MemberData(nameof(SampleLayouts))]
    public async Task Skin_switch_enqueue_and_create_all_missing_thumbnails(
        string skinName,
        ThumbnailLayoutSpec layout)
    {
        IReadOnlyList<MovieRecords> records = DiscoverTempVideos();
        if (records.Count < 3)
        {
            // ローカル実動画環境（F:\Temp）が無い CI ではスキップする。
            return;
        }

        AssertSkinJsonExists(skinName, layout);
        EnsureTestDatabase(records);
        ClearLayoutOutput(layout);

        var cache = new ThumbnailLayoutCache();
        cache.Refresh(DbName, ThumbFolder);

        var scheduler = new ThumbnailQueueScheduler();
        int buildEpoch = scheduler.TabSwitchBuildGeneration;

        await scheduler.StartTabSwitchJobAsync(
            layout,
            records,
            cache,
            WbPath,
            workGeneration: 1,
            buildEpoch,
            displayTitle: skinName).ConfigureAwait(true);

        Assert.Equal(records.Count, scheduler.Queue.Count);
        ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot();
        Assert.Equal(records.Count, snapshot.Total);

        await ProcessQueueAsync(scheduler, cache, records).ConfigureAwait(true);

        foreach (MovieRecords record in records)
        {
            string body = ThumbnailMovieNaming.GetMovieBody(record);
            string expected = cache.GetExpectedThumbPath(layout, body, record.Hash);
            Assert.True(
                File.Exists(expected),
                $"[{skinName}] thumbnail missing: {expected}");
            Assert.True(
                ThumbnailValidityHelper.LooksLikeCompositeThumbnail(expected),
                $"[{skinName}] not a composite thumbnail: {expected}");
        }

        await AssertNoPendingTabSwitchWorkAsync(layout, records, cache, skinName).ConfigureAwait(true);
    }

    private static async Task AssertNoPendingTabSwitchWorkAsync(
        ThumbnailLayoutSpec layout,
        IReadOnlyList<MovieRecords> records,
        ThumbnailLayoutCache cache,
        string skinName)
    {
        var scheduler = new ThumbnailQueueScheduler();
        int buildEpoch = scheduler.TabSwitchBuildGeneration;

        await scheduler.StartTabSwitchJobAsync(
            layout,
            records,
            cache,
            WbPath,
            workGeneration: 2,
            buildEpoch,
            displayTitle: skinName).ConfigureAwait(true);

        Assert.Empty(scheduler.Queue);
        ThumbnailJobCoordinator.Snapshot snapshot = scheduler.JobCoordinator.GetSnapshot();
        Assert.Equal(0, snapshot.Total);
    }

    [Theory]
    [InlineData("TinySample")]
    [InlineData("LargeSample")]
    public void WpfSkinLoader_loads_sample_skin_json(string skinName)
    {
        Assert.True(
            WpfSkinLoader.TryLoad(skinName, out WpfSkinDefinition definition),
            $"WpfSkinLoader failed for {skinName}");
        Assert.Equal(skinName, definition.Name);
    }

    private static void AssertSkinJsonExists(string skinName, ThumbnailLayoutSpec expectedLayout)
    {
        string skinPath = Path.Combine(ResolveRepoSkinsRoot(), skinName, "skin.json");
        Assert.True(File.Exists(skinPath), $"Missing test skin: {skinPath}");

        Assert.True(
            WpfSkinLoader.TryLoad(skinName, out WpfSkinDefinition definition),
            $"WpfSkinLoader failed for {skinName}");

        ThumbnailLayoutSpec fromSkin = ThumbnailLayoutSpec.FromWpfSkinThumbnail(definition.Thumbnail);
        Assert.Equal(expectedLayout.Key, fromSkin.Key);
    }

    private static string ResolveRepoSkinsRoot()
    {
        string fromTestOutput = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "Skins", "Wpf"));
        if (Directory.Exists(fromTestOutput))
        {
            return fromTestOutput;
        }

        return Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Skins", "Wpf"));
    }

    private static List<MovieRecords> DiscoverTempVideos()
    {
        if (!Directory.Exists(TempRoot))
        {
            return [];
        }

        string[] extensions = [".wmv", ".avi", ".mp4"];
        var paths = new List<string>();
        foreach (string extension in extensions)
        {
            string match = Directory
                .GetFiles(TempRoot, $"*{extension}", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(match))
            {
                paths.Add(match);
            }
        }

        var records = new List<MovieRecords>();
        long id = 1;
        foreach (string path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            string hash = GetHashCRC32(path);
            records.Add(new MovieRecords
            {
                Movie_Id = id++,
                Movie_Path = path,
                Movie_Name = Path.GetFileName(path),
                Hash = hash,
            });
        }

        return records;
    }

    private static void EnsureTestDatabase(IReadOnlyList<MovieRecords> records)
    {
        if (!File.Exists(WbPath))
        {
            SQLite.CreateDatabase(WbPath);
        }

        using var connection = new SQLiteConnection($"Data Source={WbPath}");
        connection.Open();

        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM movie";
            deleteCmd.ExecuteNonQuery();
        }

        var now = DateTime.Now;
        foreach (MovieRecords record in records)
        {
            var info = new FileInfo(record.Movie_Path);
            string movieName = Path.GetFileNameWithoutExtension(record.Movie_Path).ToLowerInvariant();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "INSERT INTO movie (movie_id, movie_name, movie_path, movie_length, movie_size, last_date, file_date, regist_date, hash, container, video, audio, extra, tag) " +
                "VALUES (@id, @name, @path, 0, @size, @now, @now, @now, @hash, '', '', '', '', '')";
            cmd.Parameters.AddWithValue("@id", record.Movie_Id);
            cmd.Parameters.AddWithValue("@name", movieName);
            cmd.Parameters.AddWithValue("@path", record.Movie_Path);
            cmd.Parameters.AddWithValue("@size", info.Exists ? info.Length : 0);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.Parameters.AddWithValue("@hash", record.Hash);
            cmd.ExecuteNonQuery();
        }
    }

    private static void ClearLayoutOutput(ThumbnailLayoutSpec layout)
    {
        string outPath = layout.GetOutPath(DbName, ThumbFolder);
        if (Directory.Exists(outPath))
        {
            Directory.Delete(outPath, recursive: true);
        }
    }

    private static async Task ProcessQueueAsync(
        ThumbnailQueueScheduler scheduler,
        ThumbnailLayoutCache cache,
        IReadOnlyList<MovieRecords> records)
    {
        while (true)
        {
            QueueObj item = DequeueOne(scheduler);
            if (item == null)
            {
                break;
            }

            if (!scheduler.JobCoordinator.ShouldProcess(item))
            {
                scheduler.JobCoordinator.TrySkipItem(item);
                continue;
            }

            scheduler.JobCoordinator.MarkInFlight(item);
            ThumbnailCreationHost host = CreateHost(cache, records, item);
            try
            {
                await ThumbnailCreationOrchestrator.CreateAsync(host, item).ConfigureAwait(false);
            }
            finally
            {
                scheduler.JobCoordinator.TryComplete(item);
            }
        }
    }

    private static QueueObj DequeueOne(ThumbnailQueueScheduler scheduler)
    {
        lock (scheduler.SyncRoot)
        {
            while (scheduler.Queue.TryDequeue(out QueueObj item))
            {
                if (item != null)
                {
                    return item;
                }
            }
        }

        return null;
    }

    private static ThumbnailCreationHost CreateHost(
        ThumbnailLayoutCache cache,
        IReadOnlyList<MovieRecords> records,
        QueueObj item)
    {
        return new ThumbnailCreationHost
        {
            DbFullPath = WbPath,
            DbName = DbName,
            ThumbFolder = ThumbFolder,
            LayoutCache = cache,
            RunOnUi = action => action(),
            ApplyThumbPathsOnUi = (_, _) => { },
            ApplyFailurePlaceholder = (_, _) => { },
            UpdateMovieColumn = (_, _, _) => { },
            IsSessionActive = () => true,
            FindMovieRecord = id => records.FirstOrDefault(r => r.Movie_Id == id),
        };
    }
}
