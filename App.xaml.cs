using IndigoMovieManager.Services;
using System.Windows;

namespace IndigoMovieManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            MediaExtensionSettings.EnsureRequiredExtensions();
            base.OnStartup(e);
        }
    }
}
