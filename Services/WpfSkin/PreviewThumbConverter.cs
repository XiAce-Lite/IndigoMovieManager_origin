using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// スキン編集プレビュー用。パスが空のとき columns×rows の格子ビットマップを返す。
    /// </summary>
    internal sealed class PreviewThumbConverter : IValueConverter
    {
        private readonly IValueConverter _inner;
        private int _width = 400;
        private int _height = 225;
        private int _columns = 1;
        private int _rows = 1;
        private BitmapSource _cached;
        private string _cacheKey = "";

        public PreviewThumbConverter(IValueConverter inner)
        {
            _inner = inner;
        }

        public void UpdateLayout(int width, int height, int columns, int rows)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
            _columns = Math.Clamp(columns, 1, 5);
            _rows = Math.Clamp(rows, 1, 5);
            _cacheKey = "";
            _cached = null;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (!string.IsNullOrWhiteSpace(path) && _inner != null)
            {
                object result = _inner.Convert(value, targetType, parameter, culture);
                if (result != null && result != DependencyProperty.UnsetValue && result != Binding.DoNothing)
                {
                    return result;
                }
            }

            return GetOrCreateGridBitmap();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;

        private BitmapSource GetOrCreateGridBitmap()
        {
            string key = $"{_width}x{_height}x{_columns}x{_rows}";
            if (_cached != null && _cacheKey == key)
            {
                return _cached;
            }

            int w = Math.Clamp(_width, 8, 1600);
            int h = Math.Clamp(_height, 8, 1600);
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)), null, new Rect(0, 0, w, h));

                double cellW = (double)w / _columns;
                double cellH = (double)h / _rows;
                var light = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66));
                var dark = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x44));
                var textBrush = Brushes.White;
                var typeface = new Typeface("Segoe UI");
                double fontSize = Math.Clamp(Math.Min(cellW, cellH) * 0.28, 8, 22);

                for (int row = 0; row < _rows; row++)
                {
                    for (int col = 0; col < _columns; col++)
                    {
                        var rect = new Rect(col * cellW, row * cellH, cellW, cellH);
                        bool odd = ((row + col) & 1) == 1;
                        dc.DrawRectangle(odd ? light : dark, null, rect);
                        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99)), 1), rect);

                        string label = $"{col + 1},{row + 1}";
                        var text = new FormattedText(
                            label,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            fontSize,
                            textBrush,
                            1.25);
                        dc.DrawText(
                            text,
                            new Point(
                                rect.X + (rect.Width - text.Width) / 2,
                                rect.Y + (rect.Height - text.Height) / 2));
                    }
                }
            }

            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            _cached = bitmap;
            _cacheKey = key;
            return bitmap;
        }
    }
}
