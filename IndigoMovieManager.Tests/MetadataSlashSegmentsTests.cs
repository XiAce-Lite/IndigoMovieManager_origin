using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MetadataSlashSegmentsTests
{
    [Fact]
    public void Split_returns_single_segment_when_no_separator()
    {
        IReadOnlyList<string> result = MetadataSlashSegments.Split("メーカー名");

        Assert.Single(result);
        Assert.Equal("メーカー名", result[0]);
    }

    [Fact]
    public void Split_splits_on_dmm_separator()
    {
        IReadOnlyList<string> result = MetadataSlashSegments.Split("メーカー / レーベル / シリーズ");

        Assert.Equal(3, result.Count);
        Assert.Equal("メーカー", result[0]);
        Assert.Equal("レーベル", result[1]);
        Assert.Equal("シリーズ", result[2]);
    }

    [Fact]
    public void Split_does_not_split_compact_slash()
    {
        IReadOnlyList<string> result = MetadataSlashSegments.Split("A/B");

        Assert.Single(result);
        Assert.Equal("A/B", result[0]);
    }

    [Fact]
    public void Split_returns_empty_for_blank()
    {
        Assert.Empty(MetadataSlashSegments.Split(""));
        Assert.Empty(MetadataSlashSegments.Split("   "));
        Assert.Empty(MetadataSlashSegments.Split(" / "));
    }
}
