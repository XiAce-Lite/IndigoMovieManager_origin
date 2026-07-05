using System.Windows;
using System.Windows.Controls;

namespace IndigoMovieManager.Controls
{
    internal static class SegmentRadioAssist
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached(
                "CornerRadius",
                typeof(CornerRadius),
                typeof(SegmentRadioAssist),
                new FrameworkPropertyMetadata(new CornerRadius(0)));

        public static CornerRadius GetCornerRadius(RadioButton element) =>
            (CornerRadius)element.GetValue(CornerRadiusProperty);

        public static void SetCornerRadius(RadioButton element, CornerRadius value) =>
            element.SetValue(CornerRadiusProperty, value);
    }
}
