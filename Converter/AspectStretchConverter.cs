using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IndigoMovieManager.Converter
{
    /// <summary>
    /// 画像の実アスペクト比と、表示枠の目標アスペクト比（既定 16:9）の近さで Stretch を切り替える。
    /// 目標比に近ければ UniformToFill（クロップ）、離れていれば Uniform（黒余白）。
    /// いずれも縦横比は保持し、画像が引き伸ばされることはない。
    ///   16:9 → ほぼそのまま fill / 16:10 → わずかにクロップ
    ///   4:3 や縦動画 → 左右(または上下)に黒余白
    /// </summary>
    internal sealed class AspectStretchConverter : IValueConverter
    {
        /// <summary>目標比からの相対差がこの値以下ならクロップ、超えたら黒余白。</summary>
        public double CropThreshold { get; set; } = 0.15;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not BitmapSource bmp || bmp.PixelWidth <= 0 || bmp.PixelHeight <= 0)
            {
                return Stretch.Uniform;
            }

            double sourceAspect = (double)bmp.PixelWidth / bmp.PixelHeight;
            double targetAspect = ResolveTargetAspect(parameter);
            if (targetAspect <= 0)
            {
                targetAspect = 16.0 / 9.0;
            }

            double relativeDiff = Math.Abs(sourceAspect - targetAspect) / targetAspect;
            return relativeDiff <= CropThreshold ? Stretch.UniformToFill : Stretch.Uniform;
        }

        private static double ResolveTargetAspect(object parameter)
        {
            return parameter switch
            {
                double d => d,
                float f => f,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
                _ => 16.0 / 9.0,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
