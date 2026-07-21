using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IndigoMovieManager.Converter
{
    /// <summary>
    /// null / 空文字のとき Collapsed、それ以外は Visible。
    /// </summary>
    public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
