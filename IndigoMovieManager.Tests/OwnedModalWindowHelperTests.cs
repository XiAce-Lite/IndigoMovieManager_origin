using System.Windows;
using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class OwnedModalWindowHelperTests
{
    [Fact]
    public void ExcludeFromAltTab_sets_ShowInTaskbar_false()
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window();
                Assert.True(window.ShowInTaskbar);
                OwnedModalWindowHelper.ExcludeFromAltTab(window);
                Assert.False(window.ShowInTaskbar);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
        {
            throw error;
        }
    }

    [Fact]
    public void ExcludeFromAltTab_null_is_noop()
    {
        OwnedModalWindowHelper.ExcludeFromAltTab(null);
    }
}
