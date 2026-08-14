using System.Windows.Controls;
using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ListScrollPreserverTests
{
    [Fact]
    public void RefreshListViewPreservingScroll_null_is_safe()
    {
        ListScrollPreserver.RefreshListViewPreservingScroll(null);
    }

    [StaFact]
    public void RefreshListViewPreservingScroll_empty_list_does_not_throw()
    {
        var list = new ListView();
        list.Items.Add("a");
        list.Items.Add("b");
        list.SelectedIndex = 1;

        ListScrollPreserver.RefreshListViewPreservingScroll(list);

        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void TryHandleShiftMouseWheel_null_args_return_false()
    {
        Assert.False(ListScrollPreserver.TryHandleShiftMouseWheel(null, null));
    }
}
