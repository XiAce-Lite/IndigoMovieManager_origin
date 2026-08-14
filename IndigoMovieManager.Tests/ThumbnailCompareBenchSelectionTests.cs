using IndigoMovieManager.Tests;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class ThumbnailCompareBenchSelectionTests
{
    [Fact]
    public void DiscoverBenchmarkVideos_prefers_larger_tiers_up_to_max()
    {
        string root = Path.Combine(Path.GetTempPath(), "imm-bench-select-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        long gib = 1024L * 1024 * 1024;
        string f5 = Path.Combine(root, "big5.mp4");
        string f4 = Path.Combine(root, "mid4.mp4");
        string f3 = Path.Combine(root, "low3.mp4");
        string f2 = Path.Combine(root, "tiny2.mp4");

        File.WriteAllText(f5, new string('x', 1024));
        File.WriteAllText(f4, new string('x', 1024));
        File.WriteAllText(f3, new string('x', 1024));
        File.WriteAllText(f2, new string('x', 1024));

        try
        {
            using var f5Stream = new FileStream(f5, FileMode.Open, FileAccess.ReadWrite);
            f5Stream.SetLength(5L * gib + 1);
            using var f4Stream = new FileStream(f4, FileMode.Open, FileAccess.ReadWrite);
            f4Stream.SetLength(4L * gib + 1);
            using var f3Stream = new FileStream(f3, FileMode.Open, FileAccess.ReadWrite);
            f3Stream.SetLength(3L * gib + 1);
            using var f2Stream = new FileStream(f2, FileMode.Open, FileAccess.ReadWrite);
            f2Stream.SetLength(2L * gib + 1);

            IReadOnlyList<string> selected = ThumbnailCompareBenchTests.DiscoverBenchmarkVideos(root, maxFiles: 2);
            Assert.Equal(2, selected.Count);
            Assert.Contains(f5, selected);
            Assert.Contains(f4, selected);
            Assert.DoesNotContain(f3, selected);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
