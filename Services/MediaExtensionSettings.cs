using System.IO;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 監視・フォルダ走査対象の拡張子設定。
    /// </summary>
    internal static class MediaExtensionSettings
    {
        private static readonly string[] RequiredPatterns =
        [
            ".mod",
            ".zip",
        ];

        public static void EnsureRequiredExtensions()
        {
            string current = Properties.Settings.Default.CheckExt ?? "";
            List<string> patterns = [.. ParsePatterns(current)];
            bool changed = false;

            foreach (string required in RequiredPatterns)
            {
                if (patterns.Any(p => PatternEquals(p, required)))
                {
                    continue;
                }

                patterns.Add(required);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            Properties.Settings.Default.CheckExt = NormalizeListForStorage(string.Join(",", patterns));
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// 走査対象かどうか。共通設定の対象拡張子に含まれ、かつ個別設定の除外拡張子に含まれない場合のみ true。
        /// </summary>
        public static bool ShouldScanFile(string filePath, string checkExtSetting, string excludeExtSetting = null)
        {
            if (!MatchesExtension(filePath, checkExtSetting))
            {
                return false;
            }

            return !IsExcludedExtension(filePath, excludeExtSetting, checkExtSetting);
        }

        public static bool IsExcludedExtension(string filePath, string excludeExtSetting, string checkExtSetting = null)
        {
            if (string.IsNullOrWhiteSpace(excludeExtSetting))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            IReadOnlyList<string> excluded = ParsePatterns(excludeExtSetting);
            if (!excluded.Any(p => string.Equals(p, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(checkExtSetting))
            {
                return true;
            }

            IReadOnlyList<string> allowed = ParsePatterns(checkExtSetting);
            return allowed.Any(p => string.Equals(p, extension, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeListForStorage(string extensionListSetting)
        {
            IReadOnlyList<string> patterns = ParsePatterns(extensionListSetting);
            return patterns.Count == 0 ? "" : string.Join(",", patterns);
        }

        public static bool MatchesExtension(string filePath, string checkExtSetting)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            foreach (string pattern in ParsePatterns(checkExtSetting))
            {
                string normalized = NormalizePattern(pattern);
                if (string.Equals(normalized, extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> ParsePatterns(string checkExtSetting)
        {
            if (string.IsNullOrWhiteSpace(checkExtSetting))
            {
                return [];
            }

            return checkExtSetting
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizePattern)
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static IEnumerable<string> ToEnumeratePatterns(string checkExtSetting)
        {
            foreach (string pattern in ParsePatterns(checkExtSetting))
            {
                yield return pattern.StartsWith('*') ? pattern : $"*{pattern}";
            }
        }

        private static string NormalizePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return "";
            }

            string trimmed = pattern.Trim();
            if (trimmed.StartsWith('*'))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length == 0)
            {
                return "";
            }

            return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
        }

        private static bool PatternEquals(string left, string right) =>
            string.Equals(NormalizePattern(left), NormalizePattern(right), StringComparison.OrdinalIgnoreCase);
    }
}
