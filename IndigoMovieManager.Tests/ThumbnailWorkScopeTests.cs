using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ThumbnailWorkScopeTests
{
    [Fact]
    public void CancelBatch_replaces_token_so_old_batch_is_cancelled()
    {
        var scope = new ThumbnailWorkScope();
        CancellationToken oldToken = scope.Token;
        scope.CancelBatch();
        Assert.True(oldToken.IsCancellationRequested);
        Assert.NotEqual(oldToken, scope.Token);
        Assert.False(scope.Token.IsCancellationRequested);
    }
}

public class ThumbnailProgressRegistryTests
{
    [Fact]
    public void DismissAll_disposes_registered_sessions()
    {
        ThumbnailProgressRegistry.DismissAll();
        var session = new ThumbnailProgressSession("160x120x1x1", 0);
        ThumbnailProgressRegistry.Register(session);
        ThumbnailProgressRegistry.DismissAll();
        Assert.False(session.IsVisible);
    }
}

public class MainWindowSessionStateThumbnailWorkTests
{
    [Fact]
    public void BumpThumbnailWorkGeneration_increments_value()
    {
        var state = new MainWindowSessionState();
        int before = state.ThumbnailWorkGeneration;
        Assert.Equal(before + 1, state.BumpThumbnailWorkGeneration());
    }
}
