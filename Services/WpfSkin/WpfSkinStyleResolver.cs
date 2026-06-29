using System.Collections.Generic;

namespace IndigoMovieManager.Services.WpfSkin
{
    internal sealed class ResolvedTextStyle
    {
        public double FontSize { get; set; } = 12;
        public string FontFamily { get; set; } = "";
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        // 既定は既存 Small タブ準拠（黒文字）。
        public string Foreground { get; set; } = "#000000";
        public string Background { get; set; } = "";
        public string Align { get; set; } = "left";
        public bool Wrap { get; set; }
    }

    internal static class WpfSkinStyleResolver
    {
        public static ResolvedTextStyle ResolveText(WpfSkinNode node, Dictionary<string, WpfSkinStyle> styles)
        {
            var resolved = new ResolvedTextStyle();

            if (!string.IsNullOrEmpty(node.Style)
                && styles != null
                && styles.TryGetValue(node.Style, out WpfSkinStyle named))
            {
                ApplyNamed(resolved, named);
            }

            if (node.FontSize > 0)
            {
                resolved.FontSize = node.FontSize;
            }

            if (!string.IsNullOrEmpty(node.FontFamily))
            {
                resolved.FontFamily = node.FontFamily;
            }

            if (node.Bold)
            {
                resolved.Bold = true;
            }

            if (node.Italic)
            {
                resolved.Italic = true;
            }

            if (!string.IsNullOrEmpty(node.Foreground))
            {
                resolved.Foreground = node.Foreground;
            }

            if (!string.IsNullOrEmpty(node.Background))
            {
                resolved.Background = node.Background;
            }

            if (!string.IsNullOrEmpty(node.Align))
            {
                resolved.Align = node.Align;
            }

            if (node.Wrap)
            {
                resolved.Wrap = true;
            }

            return resolved;
        }

        private static void ApplyNamed(ResolvedTextStyle target, WpfSkinStyle style)
        {
            if (style.FontSize > 0)
            {
                target.FontSize = style.FontSize;
            }

            if (!string.IsNullOrEmpty(style.FontFamily))
            {
                target.FontFamily = style.FontFamily;
            }

            if (style.Bold)
            {
                target.Bold = true;
            }

            if (style.Italic)
            {
                target.Italic = true;
            }

            if (!string.IsNullOrEmpty(style.Foreground))
            {
                target.Foreground = style.Foreground;
            }

            if (!string.IsNullOrEmpty(style.Background))
            {
                target.Background = style.Background;
            }

            if (!string.IsNullOrEmpty(style.Align))
            {
                target.Align = style.Align;
            }

            if (style.Wrap)
            {
                target.Wrap = true;
            }
        }
    }
}
