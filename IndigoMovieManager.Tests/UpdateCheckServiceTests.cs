using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class UpdateCheckServiceTests
{
    [Theory]
    [InlineData("v1.0.0.77", "1.0.0.77")]
    [InlineData("V1.0.0.76", "1.0.0.76")]
    [InlineData("1.0.0.75", "1.0.0.75")]
    public void TryParseTagVersion_accepts_release_tags(string tag, string expected)
    {
        Assert.True(UpdateCheckService.TryParseTagVersion(tag, out Version version));
        Assert.Equal(new Version(expected), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    public void TryParseTagVersion_rejects_invalid_tags(string tag)
    {
        Assert.False(UpdateCheckService.TryParseTagVersion(tag, out _));
    }

    [Fact]
    public void Newer_release_is_greater_than_current()
    {
        Assert.True(UpdateCheckService.TryParseTagVersion("v1.0.0.77", out Version latest));
        var current = new Version(1, 0, 0, 76);
        Assert.True(latest > current);
    }
}
