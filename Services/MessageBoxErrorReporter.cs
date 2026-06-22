using System.Windows;
using System.Windows.Threading;
using IndigoMovieManager.Data;

namespace IndigoMovieManager.Services
{
    internal sealed class MessageBoxErrorReporter : IDataErrorReporter
    {
        public void Report(string message, string title)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }
}
