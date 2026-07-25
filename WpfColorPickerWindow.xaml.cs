using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IndigoMovieManager
{
    /// <summary>簡易カラーピッカー（定番色スウォッチ + hex + RGB スライダー）。</summary>
    public partial class WpfColorPickerWindow : Window
    {
        private static readonly string[] PresetHex =
        [
            "#000000", "#FFFFFF", "#808080", "#FF0000", "#00AA00", "#0066FF",
            "#FFAA00", "#AA00FF", "#00AAAA", "#333333", "#CCCCCC", "#FF6699",
        ];

        public string SelectedHex { get; private set; } = "#000000";

        public WpfColorPickerWindow(Window owner, string initialHex)
        {
            InitializeComponent();
            Owner = owner;
            BuildSwatches();

            if (TryParseHex(initialHex, out byte r, out byte g, out byte b))
            {
                ApplyRgb(r, g, b, updateHexBox: true);
            }
            else
            {
                ApplyRgb(0, 0, 0, updateHexBox: true);
            }
        }

        private void BuildSwatches()
        {
            foreach (string hex in PresetHex)
            {
                if (!TryParseHex(hex, out byte r, out byte g, out byte b))
                {
                    continue;
                }

                var border = new Border
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(2),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                    Cursor = Cursors.Hand,
                    ToolTip = hex,
                    Tag = hex,
                };
                border.MouseLeftButtonUp += Swatch_Click;
                SwatchGrid.Children.Add(border);
            }
        }

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string hex
                && TryParseHex(hex, out byte r, out byte g, out byte b))
            {
                ApplyRgb(r, g, b, updateHexBox: true);
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ApplyRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value, updateHexBox: true);
        }

        private void HexBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!TryParseHex(HexBox.Text, out byte r, out byte g, out byte b))
            {
                return;
            }

            ApplyRgb(r, g, b, updateHexBox: true);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseHex(HexBox.Text, out byte r, out byte g, out byte b))
            {
                MessageBox.Show(this, "色コードが不正です（例: #RRGGBB）。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedHex = $"#{r:X2}{g:X2}{b:X2}";
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyRgb(byte r, byte g, byte b, bool updateHexBox)
        {
            SliderR.Value = r;
            SliderG.Value = g;
            SliderB.Value = b;
            if (updateHexBox)
            {
                HexBox.Text = $"#{r:X2}{g:X2}{b:X2}";
            }

            PreviewSwatch.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            LabelR.Text = r.ToString(CultureInfo.InvariantCulture);
            LabelG.Text = g.ToString(CultureInfo.InvariantCulture);
            LabelB.Text = b.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParseHex(string text, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string hex = text.Trim();
            if (hex.StartsWith('#'))
            {
                hex = hex[1..];
            }

            if (hex.Length != 6
                || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
            {
                return false;
            }

            r = (byte)((rgb >> 16) & 0xFF);
            g = (byte)((rgb >> 8) & 0xFF);
            b = (byte)(rgb & 0xFF);
            return true;
        }
    }
}
