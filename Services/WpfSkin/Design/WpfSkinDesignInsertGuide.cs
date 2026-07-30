using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IndigoMovieManager.Services.WpfSkin.Design
{
    /// <summary>
    /// stack 並べ替え時の挿入位置ライン（上下／左右）。
    /// </summary>
    internal sealed class WpfSkinDesignInsertGuide
    {
        private readonly Canvas _overlay;
        private readonly Line _line;

        public WpfSkinDesignInsertGuide()
        {
            _overlay = new Canvas
            {
                IsHitTestVisible = false,
                ClipToBounds = false,
            };
            _line = new Line
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)),
                StrokeThickness = 3,
                Visibility = Visibility.Collapsed,
                SnapsToDevicePixels = true,
            };
            _overlay.Children.Add(_line);
            _overlay.SizeChanged += (_, _) =>
            {
                if (_line.Visibility == Visibility.Visible)
                {
                    // サイズ変化時は呼び出し側が再表示する想定。消して誤位置を残さない。
                    Clear();
                }
            };
        }

        public UIElement Overlay => _overlay;

        public void Clear() => _line.Visibility = Visibility.Collapsed;

        /// <summary>
        /// after=false で先頭側、true で末尾側にラインを出す。
        /// horizontal=true なら左右、false なら上下。
        /// </summary>
        public void Show(bool after, bool horizontal)
        {
            double width = _overlay.ActualWidth;
            double height = _overlay.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                Clear();
                return;
            }

            const double pad = 2;
            if (horizontal)
            {
                double x = after ? width - pad : pad;
                _line.X1 = x;
                _line.X2 = x;
                _line.Y1 = 0;
                _line.Y2 = height;
            }
            else
            {
                double y = after ? height - pad : pad;
                _line.X1 = 0;
                _line.X2 = width;
                _line.Y1 = y;
                _line.Y2 = y;
            }

            _line.Visibility = Visibility.Visible;
        }
    }

    /// <summary>挿入位置判定（縦 stack＝Y、横 stack＝X）。</summary>
    internal static class WpfSkinDesignInsertGeometry
    {
        public static bool IsHorizontalStack(WpfSkinNode parent) =>
            parent != null
            && string.Equals(parent.Stack, "horizontal", StringComparison.OrdinalIgnoreCase);

        public static bool IsInsertAfter(Point pos, double width, double height, bool horizontal)
        {
            if (horizontal)
            {
                return width > 0 && pos.X > width / 2.0;
            }

            return height > 0 && pos.Y > height / 2.0;
        }
    }
}
