using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IndigoMovieManager
{
    /// <summary>
    /// ジャケ写の LightBox 風拡大表示。画像・背景クリックまたは Esc で閉じる。
    /// 表示サイズは元画像のピクセルサイズを超えない。
    /// </summary>
    public partial class JacketLightboxWindow : Window
    {
        public JacketLightboxWindow(ImageSource source)
        {
            InitializeComponent();
            JacketImage.Source = source;
            ApplyNativeSizeCap(source);
        }

        private void ApplyNativeSizeCap(ImageSource source)
        {
            if (source is not BitmapSource bmp || bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0)
            {
                return;
            }

            // Uniform でウィンドウいっぱいに引き伸ばされないよう、元解像度を上限にする。
            JacketImage.MaxWidth = bmp.PixelWidth;
            JacketImage.MaxHeight = bmp.PixelHeight;
            JacketImage.Stretch = Stretch.Uniform;
            JacketImage.HorizontalAlignment = HorizontalAlignment.Center;
            JacketImage.VerticalAlignment = VerticalAlignment.Center;
        }

        public static void Show(Window owner, ImageSource source)
        {
            if (source == null)
            {
                return;
            }

            var window = new JacketLightboxWindow(source)
            {
                Owner = owner,
            };
            window.ShowDialog();
        }

        public static void ShowFromUrl(Window owner, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(url.Trim(), UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                Show(owner, bmp);
            }
            catch
            {
                // 読込失敗時は何もしない
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void JacketImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }
    }
}
