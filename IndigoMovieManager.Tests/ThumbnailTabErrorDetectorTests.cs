using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailTabErrorDetectorTests
{
    private static readonly ThumbnailLayoutSpec ListLayout = new(120, 90, 3, 1);

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

    [Fact]
    public void IsErrorForLayout_returns_false_when_movie_file_missing()
    {
        var cache = CreateCache(out string thumbRoot);
        try
        {
            var record = CreateRecord("movie", @"C:\missing\movie.mp4");
            Assert.False(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache));
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
    public void IsErrorForLayout_returns_true_when_thumb_missing_but_movie_exists()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            Assert.True(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache));
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
    public void IsErrorForLayout_returns_true_when_error_placeholder_written()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                "abc123");
            string errorTemplate = cache.GetErrorPath(2);
            Assert.True(File.Exists(errorTemplate), $"error template missing: {errorTemplate}");

            string directory = Path.GetDirectoryName(expectedThumb);
            Directory.CreateDirectory(directory!);
            File.Copy(errorTemplate, expectedThumb, true);

            Assert.True(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache));
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
    public void IsErrorForLayout_returns_false_when_nofile_placeholder_written()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                "abc123");
            string noFileTemplate = cache.GetNoFilePath(2);
            Assert.True(File.Exists(noFileTemplate), $"nofile template missing: {noFileTemplate}");

            string directory = Path.GetDirectoryName(expectedThumb);
            Directory.CreateDirectory(directory!);
            File.Copy(noFileTemplate, expectedThumb, true);

            Assert.False(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache));
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
    public void IsErrorForLayout_returns_false_when_valid_composite_thumb_exists()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-movie-{Guid.NewGuid():N}.mp4");
        var cache = CreateCache(out string thumbRoot);
        try
        {
            File.WriteAllText(moviePath, "movie");
            var record = CreateRecord("movie", moviePath);

            string expectedThumb = cache.GetExpectedThumbPath(
                ListLayout,
                ThumbnailMovieNaming.GetMovieBody(record),
                "abc123");
            WriteCompositePlaceholder(expectedThumb);

            Assert.False(ThumbnailTabErrorDetector.IsErrorForLayout(record, ListLayout, cache));
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
