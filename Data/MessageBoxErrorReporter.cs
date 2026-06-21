using System.Windows;

namespace IndigoMovieManager.Data
{
  internal sealed class MessageBoxErrorReporter : IDataErrorReporter
  {
    public void Report(string message, string title)
    {
      MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }
}
