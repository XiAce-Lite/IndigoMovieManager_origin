using IndigoMovieManager.Services;
using System.Globalization;
using System.Threading;
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
            var japanese = CultureInfo.GetCultureInfo("ja");
            CultureInfo.DefaultThreadCurrentCulture = japanese;
            CultureInfo.DefaultThreadCurrentUICulture = japanese;
            Thread.CurrentThread.CurrentCulture = japanese;
            Thread.CurrentThread.CurrentUICulture = japanese;

            MediaExtensionSettings.EnsureRequiredExtensions();
            base.OnStartup(e);
        }
    }
}
