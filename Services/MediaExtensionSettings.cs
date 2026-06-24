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
            "*.mod",
            "*.zip",
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

            Properties.Settings.Default.CheckExt = string.Join(",", patterns);
            Properties.Settings.Default.Save();
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
