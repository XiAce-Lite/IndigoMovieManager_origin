using System;
using System.Collections.Generic;
using System.Linq;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// スキン名の表示順を統一するヘルパー。
    /// Default 系（"Default" で始まる名前）を必ず先頭に、その中は名称順。
    /// それ以降（サンプル等）も名称順で並べる。
    /// </summary>
    internal static class SkinNameSortHelper
    {
        private const string DefaultPrefix = "Default";

        /// <summary>
        /// アプリからの上書き・削除を禁止する同梱 Default 系か。
        /// フォルダ名が Default で始まるもの（大文字小文字無視）。
        /// </summary>
        public static bool IsProtectedDefaultSkin(string name) =>
            !string.IsNullOrWhiteSpace(name)
            && name.StartsWith(DefaultPrefix, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> OrderDefaultFirst(IEnumerable<string> names)
        {
            if (names == null)
            {
                return Array.Empty<string>();
            }

            return names
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n.StartsWith(DefaultPrefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
