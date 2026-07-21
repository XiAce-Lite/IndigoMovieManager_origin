using IndigoMovieManager.Services.Dmm;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DmmMetadataEligibilityTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData(null, null, true)]
    [InlineData("  ", "  ", true)]
    [InlineData("タイトル", "", false)]
    [InlineData("", "コメント", false)]
    [InlineData("タイトル", "コメント", false)]
    public void NeedsFetch_checks_title_and_comment1(string title, string comment1, bool expected)
    {
        Assert.Equal(expected, DmmMetadataEligibility.NeedsFetch(title, comment1));
    }
}

public class DmmAutoFetchQueueTests
{
    private sealed class FakeHost : IDmmAutoFetchHost
    {
        public bool IsManualFetchRunning { get; set; }

        public List<string> CompletionMessages { get; } = [];

        public void RunOnUi(Action action) => action();

        public Task RunOnUiAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public MovieRecords FindMovieRecord(long movieId) => null;

        public void NotifyRecordUpdated(long movieId)
        {
        }

        public void NotifyPendingCandidatesChanged()
        {
        }

        public void ShowCompletionMessage(string message) => CompletionMessages.Add(message);

        public List<(string Title, string Message)> CompletionDialogs { get; } = [];

        public void ShowCompletionDialog(string title, string message) =>
            CompletionDialogs.Add((title, message));
    }

    [Fact]
    public void Enqueue_ignores_duplicate_movie_id()
    {
        var host = new FakeHost();
        using var queue = new DmmAutoFetchQueue(host);

        Assert.True(queue.Enqueue(1, "a.mp4", @"C:\db.wb"));
        Assert.False(queue.Enqueue(1, "a.mp4", @"C:\db.wb"));
    }

    [Fact]
    public void EnqueueMany_counts_only_new_items()
    {
        var host = new FakeHost();
        using var queue = new DmmAutoFetchQueue(host);

        int added = queue.EnqueueMany(
        [
            new DmmAutoFetchJob { MovieId = 1, MovieName = "a.mp4", DbPath = @"C:\db.wb", Source = "bulk" },
            new DmmAutoFetchJob { MovieId = 1, MovieName = "a.mp4", DbPath = @"C:\db.wb", Source = "bulk" },
            new DmmAutoFetchJob { MovieId = 2, MovieName = "b.mp4", DbPath = @"C:\db.wb", Source = "bulk" },
        ]);

        Assert.Equal(2, added);
    }
}
