using System.Windows.Threading;

namespace IndigoMovieManager.Services
{
    internal static class UiDispatcherHelper
    {
        public static void RunOnUi(Dispatcher dispatcher, Action action)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        public static Task RunOnUiAsync(Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
        {
            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, priority).Task;
        }
    }
}
