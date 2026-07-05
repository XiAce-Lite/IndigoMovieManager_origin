using System.Collections.Generic;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// colorProfile 未指定スキンの色をアプリダーク時に変換する。
    /// </summary>
    internal static class WpfSkinColorRemap
    {
        private static readonly Dictionary<string, string> LightToDark = new(StringComparer.OrdinalIgnoreCase)
        {
            ["#FFFFFF"] = "#1E1E1E",
            ["#FFF"] = "#1E1E1E",
            ["#000000"] = "#E0E0E0",
            ["#000"] = "#E0E0E0",
            ["#555555"] = "#AAAAAA",
            ["#555"] = "#AAAAAA",
            ["#888888"] = "#999999",
            ["#888"] = "#999999",
            ["#F0F0F0"] = "#2D2D2D",
        };

        public static string RemapIfKnown(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return hexColor;
            }

            string normalized = NormalizeHex(hexColor);
            return LightToDark.TryGetValue(normalized, out string mapped)
                ? mapped
                : hexColor;
        }

        public static string NormalizeHex(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return "";
            }

            string trimmed = hexColor.Trim();
            if (!trimmed.StartsWith('#'))
            {
                trimmed = "#" + trimmed;
            }

            if (trimmed.Length == 4)
            {
                return $"#{char.ToUpperInvariant(trimmed[1])}{char.ToUpperInvariant(trimmed[1])}" +
                       $"{char.ToUpperInvariant(trimmed[2])}{char.ToUpperInvariant(trimmed[2])}" +
                       $"{char.ToUpperInvariant(trimmed[3])}{char.ToUpperInvariant(trimmed[3])}";
            }

            return trimmed.ToUpperInvariant();
        }
    }
}
