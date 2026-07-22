using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class StartupDocumentResolverTests
{
    [Fact]
    public void Resolve_returns_null_when_no_wb_arg()
    {
        Assert.Null(StartupDocumentResolver.Resolve(null));
        Assert.Null(StartupDocumentResolver.Resolve([]));
        Assert.Null(StartupDocumentResolver.Resolve(["--help", @"C:\temp\note.txt"]));
    }

    [Fact]
    public void Resolve_picks_first_wb_path()
    {
        string path = StartupDocumentResolver.Resolve(
        [
            @"C:\libs\first.wb",
            @"C:\libs\second.wb"
        ]);

        Assert.Equal(@"C:\libs\first.wb", path);
    }

    [Fact]
    public void Resolve_accepts_quoted_and_case_insensitive_extension()
    {
        string path = StartupDocumentResolver.Resolve([@"""D:\Data\Sample.WB"""]);
        Assert.Equal(@"D:\Data\Sample.WB", path);
    }
}
