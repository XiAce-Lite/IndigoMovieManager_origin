using System;
using System.Linq;
using System.Windows.Media;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// skin.json の fontFamily を解決する。未インストール名は既定フォントへ戻す。
    /// </summary>
    internal static class WpfSkinFontResolver
    {
        public const string DefaultFontFamily = "Yu Gothic UI";

        /// <summary>空・未インストールなら <see cref="DefaultFontFamily"/> を返す。</summary>
        public static string ResolveFamilyName(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested))
            {
                return DefaultFontFamily;
            }

            string name = requested.Trim();
            return IsInstalled(name) ? name : DefaultFontFamily;
        }

        public static bool IsInstalled(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                return false;
            }

            string target = familyName.Trim();
            foreach (FontFamily family in Fonts.SystemFontFamilies)
            {
                if (string.Equals(family.Source, target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (family.FamilyNames.Values.Any(n =>
                        string.Equals(n, target, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
