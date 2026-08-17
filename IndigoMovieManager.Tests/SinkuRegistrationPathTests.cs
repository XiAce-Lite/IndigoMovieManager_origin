using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class SinkuRegistrationPathTests
{
    [Fact]
    public void ResolveInsertMediaFields_zip_skips_sinku_and_keeps_image_count()
    {
        string zipPath = Path.Combine(Path.GetTempPath(), $"imm-sinku-zip-{Guid.NewGuid():N}.zip");
        File.WriteAllBytes(zipPath, [0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        try
        {
            var info = new MovieInfo(zipPath, noHash: true);
            SQLite.ResolveInsertMediaFields(
                info,
                out string container,
                out string video,
                out string audio,
                out string extra,
                out long length);

            Assert.Equal("zip", container);
            Assert.Equal("", video);
            Assert.Equal("", audio);
            Assert.Equal("", extra);
            Assert.Equal(info.MovieLength, length);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public void TryFetch_returns_false_when_sinku_unavailable_without_throwing()
    {
        // テスト出力に sinku が無くても落ちないこと（本番は exe 横に配置）
        if (SinkuMetadataFetcher.IsAvailable)
        {
            return;
        }

        Assert.False(SinkuMetadataFetcher.TryFetch(
            Path.Combine(Path.GetTempPath(), "missing-movie.mp4"),
            out SinkuMetadata metadata));
        Assert.Null(metadata);
    }
}
