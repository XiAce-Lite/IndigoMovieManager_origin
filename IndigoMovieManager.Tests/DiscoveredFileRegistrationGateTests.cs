using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class DiscoveredFileRegistrationGateTests
{
    [Fact]
    public void TryEnter_allows_first_path_and_blocks_duplicate_until_exit()
    {
        var gate = new DiscoveredFileRegistrationGate();

        Assert.True(gate.TryEnter(@"C:\movies\sample.mp4"));
        Assert.False(gate.TryEnter(@"C:\movies\sample.mp4"));
        Assert.False(gate.TryEnter(@"c:\movies\SAMPLE.mp4"));

        gate.Exit(@"C:\movies\sample.mp4");

        Assert.True(gate.TryEnter(@"c:\movies\sample.mp4"));
        gate.Exit(@"c:\movies\sample.mp4");
    }

    [Fact]
    public void Clear_releases_all_in_flight_paths()
    {
        var gate = new DiscoveredFileRegistrationGate();

        Assert.True(gate.TryEnter(@"C:\a.mp4"));
        Assert.True(gate.TryEnter(@"C:\b.mp4"));

        gate.Clear();

        Assert.True(gate.TryEnter(@"C:\a.mp4"));
        gate.Exit(@"C:\a.mp4");
    }
}
