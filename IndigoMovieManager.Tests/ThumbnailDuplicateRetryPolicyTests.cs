using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class ThumbnailDuplicateRetryPolicyTests
{
    [Fact]
    public void ShouldRetryOpenCvPerPanel_true_only_for_manual()
    {
        Assert.True(ThumbnailDuplicateRetryPolicy.ShouldRetryOpenCvPerPanel(isManual: true));
        Assert.False(ThumbnailDuplicateRetryPolicy.ShouldRetryOpenCvPerPanel(isManual: false));
    }
}
