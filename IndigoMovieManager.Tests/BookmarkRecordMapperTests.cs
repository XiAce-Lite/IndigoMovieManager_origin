using Xunit;

namespace IndigoMovieManager.Tests;

public class BookmarkRecordMapperTests
{
    [Fact]
    public void ResolveSourceExists_returns_false_when_comment1_empty()
    {
        Assert.False(BookmarkRecordMapper.ResolveSourceExists(""));
        Assert.False(BookmarkRecordMapper.ResolveSourceExists("   "));
        Assert.False(BookmarkRecordMapper.ResolveSourceExists(null));
    }

    [Fact]
    public void ResolveSourceExists_returns_false_when_path_missing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"imm-bm-missing-{Guid.NewGuid():N}.mp4");

        Assert.False(BookmarkRecordMapper.ResolveSourceExists(missing));
    }

    [Fact]
    public void ResolveSourceExists_returns_true_when_path_exists()
    {
        string path = Path.Combine(Path.GetTempPath(), $"imm-bm-exists-{Guid.NewGuid():N}.mp4");
        File.WriteAllText(path, "x");

        try
        {
            Assert.True(BookmarkRecordMapper.ResolveSourceExists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
