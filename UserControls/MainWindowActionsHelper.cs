using System.Windows;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.UserControls
{
    internal static class MainWindowActionsHelper
    {
        public static IMainWindowActions GetActions(DependencyObject control)
        {
            return Window.GetWindow(control) as IMainWindowActions;
        }
    }
}
