using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailHashSyncTests
{
    private const string OldHash = "oldhash12";
    private const string NewHash = "newhash99";
    private const string MatchedHash = "abc123";

    private static readonly ThumbnailLayoutSpec ListLayout = new(120, 90, 3, 1);

    private static MovieRecords CreateRecord(string name, string path, string hash = OldHash) =>
        new()
        {
            Movie_Id = 42,
            Movie_Name = name,
            Movie_Path = path,
            Hash = hash,
        };

    private static ThumbnailLayoutCache CreateCache(out string thumbRoot)
    {
        thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-hashsync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        var cache = new ThumbnailLayoutCache();
        cache.Refresh("testdb", thumbRoot);
        return cache;
    }

    private static void WriteCompositePlaceholder(string path)
    {
        byte[] data = new byte[128];
        BitConverter.GetBytes((ushort)1).CopyTo(data, data.Length - 60);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, data);
    }

    private static ThumbnailHashSyncContext CreateMockContext(List<(long MovieId, string Hash)> dbUpdates) =>
        new()
        {
            DbFullPath = @"C:\fake\db.wb",
            ComputeFileHash = _ => NewHash,
            UpdateDbHash = (movieId, hash) => dbUpdates.Add((movieId, hash)),
        };

    [Fact]
    public void IsErrorForLayout_returns_false_and_syncs_when_new_hash_thumb_exists()
    {
        ThumbnailHashSync.ClearFileHashCache();
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        var dbUpdates = new List<(long MovieId, string Hash)>();
        var context = CreateMockContext(dbUpdates);

        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string newHashPath = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                NewHash);
            WriteCompositePlaceholder(newHashPath);

            Assert.False(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache, context));
            Assert.Equal(NewHash, record.Hash);
            Assert.Single(dbUpdates);
            Assert.Equal((42L, NewHash), dbUpdates[0]);
        }
        finally
        {
            ThumbnailHashSync.ClearFileHashCache();
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
    public void IsErrorForLayout_returns_true_and_syncs_when_no_thumb_exists()
    {
        ThumbnailHashSync.ClearFileHashCache();
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        var dbUpdates = new List<(long MovieId, string Hash)>();
        var context = CreateMockContext(dbUpdates);

        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            Assert.True(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache, context));
            Assert.Equal(NewHash, record.Hash);
            Assert.Single(dbUpdates);
            Assert.Equal((42L, NewHash), dbUpdates[0]);
        }
        finally
        {
            ThumbnailHashSync.ClearFileHashCache();
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
    public void ShouldEnqueueAfterHashSync_returns_false_when_new_hash_thumb_exists()
    {
        ThumbnailHashSync.ClearFileHashCache();
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        var dbUpdates = new List<(long MovieId, string Hash)>();
        var context = CreateMockContext(dbUpdates);

        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string newHashPath = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                NewHash);
            WriteCompositePlaceholder(newHashPath);

            Assert.False(ThumbnailHashSync.ShouldEnqueueAfterHashSync(record, ListLayout, cache, context));
            Assert.Equal(NewHash, record.Hash);
            Assert.Single(dbUpdates);
        }
        finally
        {
            ThumbnailHashSync.ClearFileHashCache();
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
    public void ResolveHashForThumbnail_does_not_call_db_when_hash_matches_existing_thumb()
    {
        ThumbnailHashSync.ClearFileHashCache();
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        var dbUpdates = new List<(long MovieId, string Hash)>();
        var context = new ThumbnailHashSyncContext
        {
            DbFullPath = @"C:\fake\db.wb",
            ComputeFileHash = _ => MatchedHash,
            UpdateDbHash = (movieId, hash) => dbUpdates.Add((movieId, hash)),
        };

        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath, MatchedHash);

            string expectedThumb = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                MatchedHash);
            WriteCompositePlaceholder(expectedThumb);

            string resolved = ThumbnailHashSync.ResolveHashForThumbnail(
                record,
                ListLayout,
                cache,
                context);

            Assert.Equal(MatchedHash, resolved);
            Assert.Equal(MatchedHash, record.Hash);
            Assert.Empty(dbUpdates);
        }
        finally
        {
            ThumbnailHashSync.ClearFileHashCache();
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
}
