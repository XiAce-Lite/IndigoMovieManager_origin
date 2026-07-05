using IndigoMovieManager.Services;
using System.Globalization;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

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
            AppThemeService.InitializeFromSettings();
            SystemEvents.UserPreferenceChanged += App_SystemPreferenceChanged;
            base.OnStartup(e);
        }

        private static void App_SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                AppThemeService.HandleSystemThemeChanged();
            }
        }
    }
}
