using System.Runtime.InteropServices;
using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class ClipboardAccessTests
{
    [Fact]
    public void IsClipboardBusyException_detects_clipbrd_cant_open()
    {
        var ex = new COMException("OpenClipboard failed", unchecked((int)0x800401D0));
        Assert.True(ClipboardAccess.IsClipboardBusyException(ex));
        Assert.True(ClipboardAccess.IsClipboardBusyException(new InvalidOperationException("wrap", ex)));
    }

    [Fact]
    public void IsClipboardBusyException_ignores_other_errors()
    {
        Assert.False(ClipboardAccess.IsClipboardBusyException(new COMException("other", unchecked((int)0x80004005))));
        Assert.False(ClipboardAccess.IsClipboardBusyException(new InvalidOperationException("x")));
        Assert.False(ClipboardAccess.IsClipboardBusyException(null));
    }
}
