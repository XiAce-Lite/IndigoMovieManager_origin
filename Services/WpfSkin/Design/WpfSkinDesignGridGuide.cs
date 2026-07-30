using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IndigoMovieManager.Services.WpfSkin.Design
{
    /// <summary>
    /// デザイン時プレビュー用の grid セル幾何。本番描画には使わない。
    /// </summary>
    internal static class WpfSkinDesignGridGeometry
    {
        public static bool IsGridPanel(WpfSkinNode node) =>
            node != null
            && string.Equals(node.Panel, "grid", StringComparison.OrdinalIgnoreCase);

        public static int ResolveRowCount(WpfSkinNode gridNode, int fallback = 1)
        {
            int fromDef = gridNode?.Rows?.Count ?? 0;
            return Math.Max(1, fromDef > 0 ? fromDef : Math.Max(1, fallback));
        }

        public static int ResolveColumnCount(WpfSkinNode gridNode, int fallback = 1)
        {
            int fromDef = gridNode?.Columns?.Count ?? 0;
            return Math.Max(1, fromDef > 0 ? fromDef : Math.Max(1, fallback));
        }

        public static int HitIndex(double position, double total, int count, IList<double> sizes)
        {
            if (count <= 1)
            {
                return 0;
            }

            if (sizes != null && sizes.Count == count && sizes.All(v => v > 0))
            {
                double acc = 0;
                for (int i = 0; i < count; i++)
                {
                    acc += sizes[i];
                    if (position <= acc)
                    {
                        return i;
                    }
                }

                return count - 1;
            }

            double slot = total / count;
            if (slot <= 0)
            {
                return 0;
            }

            return Math.Clamp((int)(position / slot), 0, count - 1);
        }

        public static bool TryGetCellRect(
            double width,
            double height,
            int rows,
            int cols,
            int row,
            int col,
            IList<double> rowSizes,
            IList<double> colSizes,
            out Rect rect)
        {
            rect = default;
            rows = Math.Max(1, rows);
            cols = Math.Max(1, cols);
            if (row < 0 || col < 0 || row >= rows || col >= cols || width <= 0 || height <= 0)
            {
                return false;
            }

            double x = SumPrefix(colSizes, cols, width, col);
            double y = SumPrefix(rowSizes, rows, height, row);
            double w = SliceSize(colSizes, cols, width, col);
            double h = SliceSize(rowSizes, rows, height, row);
            if (w <= 0 || h <= 0)
            {
                return false;
            }

            rect = new Rect(x, y, w, h);
            return true;
        }

        private static double SumPrefix(IList<double> sizes, int count, double total, int index)
        {
            if (sizes != null && sizes.Count == count && sizes.All(v => v > 0))
            {
                double sum = 0;
                for (int i = 0; i < index; i++)
                {
                    sum += sizes[i];
                }

                return sum;
            }

            return total / count * index;
        }

        private static double SliceSize(IList<double> sizes, int count, double total, int index)
        {
            if (sizes != null && sizes.Count == count && sizes.All(v => v > 0))
            {
                return sizes[index];
            }

            return total / count;
        }
    }

    /// <summary>
    /// grid ノード上にセル境界とドロップ中ハイライトを重ねる。
    /// </summary>
    internal sealed class WpfSkinDesignGridGuide
    {
        private readonly WpfSkinNode _gridNode;
        private readonly Canvas _overlay;
        private readonly Rectangle _highlight;
        private readonly List<Line> _lines = [];

        public WpfSkinDesignGridGuide(WpfSkinNode gridNode)
        {
            _gridNode = gridNode ?? throw new ArgumentNullException(nameof(gridNode));
            _overlay = new Canvas
            {
                IsHitTestVisible = false,
                ClipToBounds = true,
            };
            _highlight = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(0x44, 0x43, 0xA0, 0x47)),
                Stroke = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)),
                StrokeThickness = 1.5,
                Visibility = Visibility.Collapsed,
            };
            _overlay.Children.Add(_highlight);
            _overlay.SizeChanged += (_, _) => RebuildLines();
            RebuildLines();
        }

        public UIElement Overlay => _overlay;

        public void ClearHighlight() => _highlight.Visibility = Visibility.Collapsed;

        public void HighlightCell(int row, int col)
        {
            int rows = WpfSkinDesignGridGeometry.ResolveRowCount(_gridNode);
            int cols = WpfSkinDesignGridGeometry.ResolveColumnCount(_gridNode);
            if (!WpfSkinDesignGridGeometry.TryGetCellRect(
                    _overlay.ActualWidth,
                    _overlay.ActualHeight,
                    rows,
                    cols,
                    row,
                    col,
                    null,
                    null,
                    out Rect rect))
            {
                ClearHighlight();
                return;
            }

            Canvas.SetLeft(_highlight, rect.X);
            Canvas.SetTop(_highlight, rect.Y);
            _highlight.Width = rect.Width;
            _highlight.Height = rect.Height;
            _highlight.Visibility = Visibility.Visible;
        }

        private void RebuildLines()
        {
            foreach (Line line in _lines)
            {
                _overlay.Children.Remove(line);
            }

            _lines.Clear();

            double width = _overlay.ActualWidth;
            double height = _overlay.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            int rows = WpfSkinDesignGridGeometry.ResolveRowCount(_gridNode);
            int cols = WpfSkinDesignGridGeometry.ResolveColumnCount(_gridNode);
            var brush = new SolidColorBrush(Color.FromArgb(0x99, 0x1E, 0x88, 0xE5));

            for (int c = 1; c < cols; c++)
            {
                double x = width / cols * c;
                var line = CreateLine(x, 0, x, height, brush);
                _lines.Add(line);
                _overlay.Children.Add(line);
            }

            for (int r = 1; r < rows; r++)
            {
                double y = height / rows * r;
                var line = CreateLine(0, y, width, y, brush);
                _lines.Add(line);
                _overlay.Children.Add(line);
            }
        }

        private static Line CreateLine(double x1, double y1, double x2, double y2, Brush brush) =>
            new()
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = brush,
                StrokeThickness = 1,
                StrokeDashArray = [3, 2],
                SnapsToDevicePixels = true,
            };
    }
}
