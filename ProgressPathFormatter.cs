using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace IndigoMovieManager
{
    /// <summary>
    /// 進捗ポップアップ向けに、1行に収まるパス文字列を組み立てる。
    /// </summary>
    internal static class ProgressPathFormatter
    {
        private const string Ellipsis = "...";
        private const int SafeSingleLineChars = 46;
        private static readonly Typeface MessageTypeface = new("Segoe UI");

        public static string Format(string fullPath, double maxTextWidth)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return string.Empty;
            }

            fullPath = fullPath.Trim();
            double widthLimit = Math.Max(120d, maxTextWidth);
            if (fullPath.Length <= SafeSingleLineChars && MeasureWidth(fullPath) <= widthLimit)
            {
                return fullPath;
            }

            string fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = fullPath;
            }

            if (MeasureWidth(fileName) >= widthLimit)
            {
                return TrimEndToWidth(fileName, widthLimit);
            }

            int dirLength = fullPath.Length - fileName.Length;
            if (dirLength <= 0)
            {
                return fileName;
            }

            string dirPart = fullPath.Substring(0, dirLength);
            int lo = 0;
            int hi = dirPart.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                string candidate = dirPart.Substring(0, mid) + Ellipsis + fileName;
                if (MeasureWidth(candidate) <= widthLimit)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            if (lo <= 0)
            {
                return Ellipsis + fileName;
            }

            return dirPart.Substring(0, lo) + Ellipsis + fileName;
        }

        private static string TrimEndToWidth(string text, double maxTextWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (MeasureWidth(text) <= maxTextWidth)
            {
                return text;
            }

            int lo = 0;
            int hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                string candidate = text.Substring(0, mid) + Ellipsis;
                if (MeasureWidth(candidate) <= maxTextWidth)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo <= 0 ? Ellipsis : text.Substring(0, lo) + Ellipsis;
        }

        private static double MeasureWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0d;
            }

            double pixelsPerDip = 1.0;
            if (Application.Current?.MainWindow != null)
            {
                pixelsPerDip = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
            }

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                MessageTypeface,
                ThumbnailProgressSession.MessageFontSize,
                Brushes.Black,
                pixelsPerDip);

            return formattedText.WidthIncludingTrailingWhitespace;
        }
    }
}
