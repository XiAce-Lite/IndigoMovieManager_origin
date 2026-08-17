using IndigoMovieManager.Services;
using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Threading;

namespace IndigoMovieManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 起動引数で指定された .wb（無ければ null）。LastDoc より優先して開く。
        /// </summary>
        public static string StartupDocumentPath { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppFileLogger.Initialize();
            AppFileLogger.LogInfo("startup", "application starting");
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            var japanese = CultureInfo.GetCultureInfo("ja");
            CultureInfo.DefaultThreadCurrentCulture = japanese;
            CultureInfo.DefaultThreadCurrentUICulture = japanese;
            Thread.CurrentThread.CurrentCulture = japanese;
            Thread.CurrentThread.CurrentUICulture = japanese;

            StartupDocumentPath = StartupDocumentResolver.Resolve(e.Args);

            MediaExtensionSettings.EnsureRequiredExtensions();
            AppThemeService.InitializeFromSettings();
            SystemEvents.UserPreferenceChanged += App_SystemPreferenceChanged;
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
            base.OnExit(e);
        }

        private static void App_SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                AppThemeService.HandleSystemThemeChanged();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
            {
                AppFileLogger.LogError(exception, "AppDomain.UnhandledException");
                return;
            }

            AppFileLogger.LogError("AppDomain.UnhandledException", "non-Exception object");
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            AppFileLogger.LogError(e.Exception, "TaskScheduler.UnobservedTaskException");
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppFileLogger.LogError(e.Exception, "Application.DispatcherUnhandledException");

            // クリップボード占有による一時失敗はアプリ終了にしない
            if (ClipboardAccess.IsClipboardBusyException(e.Exception))
            {
                e.Handled = true;
            }
        }
    }
}
