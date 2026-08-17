using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class MetadataClipboardTests
{
    [Fact]
    public void RoundTrip_preserves_fields_and_newlines()
    {
        var src = new MetadataEditModel
        {
            Title = "タイトル\n二行",
            Comment1 = @"https://example.invalid/a=b",
            Comment2 = "c2",
            Comment3 = "maker/label/series",
            Artist = "メーカー",
            Genre = "a|b|c",
        };

        Assert.True(MetadataClipboard.TryDeserialize(MetadataClipboard.Serialize(src), out MetadataEditModel dst));
        Assert.Equal(src.Title, dst.Title);
        Assert.Equal(src.Comment1, dst.Comment1);
        Assert.Equal(src.Comment2, dst.Comment2);
        Assert.Equal(src.Comment3, dst.Comment3);
        Assert.Equal(src.Artist, dst.Artist);
        Assert.Equal(src.Genre, dst.Genre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-metadata")]
    [InlineData("Title=only")]
    public void TryDeserialize_rejects_non_payload(string text)
    {
        Assert.False(MetadataClipboard.TryDeserialize(text, out _));
    }
}
