using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class BookmarkThumbnailRestoreServiceTests
{
    [Fact]
    public void TryPrepareRestore_returns_false_when_thumb_file_exists()
    {
        string thumbPath = Path.Combine(Path.GetTempPath(), $"imm-bm-thumb-{Guid.NewGuid():N}.jpg");
        File.WriteAllText(thumbPath, "thumb");

        var bookmark = new MovieRecords
        {
            ThumbDetail = thumbPath,
            Comment1 = @"D:\movies\sample.mp4",
            Score = 300,
        };

        try
        {
            bool prepared = BookmarkThumbnailRestoreService.TryPrepareRestore(
                bookmark,
                [],
                out _,
                out _,
                out _);

            Assert.False(prepared);
        }
        finally
        {
            File.Delete(thumbPath);
        }
    }

    [Fact]
    public void TryPrepareRestore_returns_false_when_source_movie_missing()
    {
        string thumbPath = Path.Combine(Path.GetTempPath(), $"imm-bm-thumb-{Guid.NewGuid():N}.jpg");

        var bookmark = new MovieRecords
        {
            ThumbDetail = thumbPath,
            Comment1 = @"D:\missing\sample.mp4",
            Score = 300,
        };

        bool prepared = BookmarkThumbnailRestoreService.TryPrepareRestore(
            bookmark,
            [],
            out _,
            out _,
            out _);

        Assert.False(prepared);
    }

    [Fact]
    public void TryPrepareRestore_returns_capture_position_from_frame_and_fps()
    {
        string moviePath = Path.Combine(Path.GetTempPath(), $"imm-bm-movie-{Guid.NewGuid():N}.mp4");
        string thumbPath = Path.Combine(Path.GetTempPath(), $"imm-bm-thumb-{Guid.NewGuid():N}.jpg");
        File.WriteAllText(moviePath, "movie");

        var bookmark = new MovieRecords
        {
            ThumbDetail = thumbPath,
            Comment1 = moviePath,
            Score = 300,
        };

        try
        {
            bool prepared = BookmarkThumbnailRestoreService.TryPrepareRestore(
                bookmark,
                [],
                out string sourceMoviePath,
                out string saveThumbPath,
                out int capturePosSeconds);

            Assert.True(prepared);
            Assert.Equal(moviePath, sourceMoviePath);
            Assert.Equal(thumbPath, saveThumbPath);
            Assert.Equal(10, capturePosSeconds);
        }
        finally
        {
            File.Delete(moviePath);
        }
    }
}
