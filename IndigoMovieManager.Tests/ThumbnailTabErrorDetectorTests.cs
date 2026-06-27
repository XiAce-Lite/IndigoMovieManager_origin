using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailTabErrorDetectorTests
{
    private static MovieRecords CreateRecord(string name, string path, string hash = "abc123") =>
        new()
        {
            Movie_Id = 1,
            Movie_Name = name,
            Movie_Path = path,
            Hash = hash,
        };

    private static ThumbnailLayoutCache CreateCache(out string thumbRoot)
    {
        thumbRoot = Path.Combine(Path.GetTempPath(), $"imm-err-{Guid.NewGuid():N}");
        Directory.CreateDirectory(thumbRoot);
        var cache = new ThumbnailLayoutCache();
        cache.Refresh("testdb", thumbRoot, 5);
        return cache;
    }

    private static string GetExpectedThumbPath(ThumbnailLayoutCache cache, int tabIndex, string movieName, string hash) =>
        cache.GetExpectedThumbPath(
            tabIndex,
            Path.GetFileNameWithoutExtension(movieName).ToLowerInvariant(),
            hash);

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

    [Fact]
    public void IsErrorForTab_returns_false_when_movie_file_missing()
    {
        var cache = CreateCache(out string thumbRoot);
        try
        {
            var record = CreateRecord("movie", @"C:\missing\movie.mp4");
            Assert.False(ThumbnailTabErrorDetector.IsErrorForTab(record, 0, cache));
        }
        finally
        {
            if (Directory.Exists(thumbRoot))
            {
                Directory.Delete(thumbRoot, true);
            }
        }
    }

    [Fact]
    public void IsErrorForTab_returns_true_when_thumb_missing_but_movie_exists()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            Assert.True(ThumbnailTabErrorDetector.IsErrorForTab(record, 0, cache));
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
    public void IsErrorForTab_returns_true_when_error_placeholder_written()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = GetExpectedThumbPath(cache, 0, "movie", "abc123");
            string errorTemplate = cache.GetErrorPath(0);
            Assert.True(File.Exists(errorTemplate), $"error template missing: {errorTemplate}");

            string directory = Path.GetDirectoryName(expectedThumb);
            Directory.CreateDirectory(directory!);
            File.Copy(errorTemplate, expectedThumb, true);

            Assert.True(ThumbnailTabErrorDetector.IsErrorForTab(record, 0, cache));
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
    public void IsErrorForTab_returns_false_when_nofile_placeholder_written()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = GetExpectedThumbPath(cache, 0, "movie", "abc123");
            string noFileTemplate = cache.GetNoFilePath(0);
            Assert.True(File.Exists(noFileTemplate), $"nofile template missing: {noFileTemplate}");

            string directory = Path.GetDirectoryName(expectedThumb);
            Directory.CreateDirectory(directory!);
            File.Copy(noFileTemplate, expectedThumb, true);

            Assert.False(ThumbnailTabErrorDetector.IsErrorForTab(record, 0, cache));
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
    public void IsDetailThumbnailError_returns_true_when_detail_thumb_missing()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            Assert.True(ThumbnailTabErrorDetector.IsDetailThumbnailError(record, cache));
            Assert.True(ThumbnailTabErrorDetector.IsErrorForTab(record, 99, cache));
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
    public void IsErrorForTab_returns_false_when_valid_composite_thumb_exists()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = GetExpectedThumbPath(cache, 0, "movie", "abc123");
            WriteCompositePlaceholder(expectedThumb);

            Assert.False(ThumbnailTabErrorDetector.IsErrorForTab(record, 0, cache));
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
}
