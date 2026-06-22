using IndigoMovieManager.Data;

namespace IndigoMovieManager.Services
{
    internal static class UiErrorReporter
    {
        private static readonly IDataErrorReporter Reporter = new MessageBoxErrorReporter();

        public static void ShowError(string message, string title) =>
            Reporter.Report(message, title);
    }
}
