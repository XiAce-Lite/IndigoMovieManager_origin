using IndigoMovieManager.Services;

namespace IndigoMovieManager.Services.WpfSkin
{
    internal static class WpfSkinColorResolver
    {
        public static bool IsJsonAuthoritative(WpfSkinDefinition definition) =>
            !string.IsNullOrWhiteSpace(definition?.ColorProfile);

        public static string ResolveColor(string hexColor, WpfSkinDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return hexColor;
            }

            if (IsJsonAuthoritative(definition) || !AppThemeService.IsDarkEffective)
            {
                return hexColor;
            }

            return WpfSkinColorRemap.RemapIfKnown(hexColor);
        }

        public static System.Windows.Media.Brush ResolveBrush(
            string hexColor,
            System.Windows.Media.Brush fallback,
            WpfSkinDefinition definition)
        {
            string resolved = ResolveColor(hexColor, definition);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return fallback;
            }

            try
            {
                var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                    .ConvertFromString(resolved);
                brush?.Freeze();
                return brush ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
