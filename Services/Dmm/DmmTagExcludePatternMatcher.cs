using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// DMM ジャンル→タグ除外パターン。簡易ワイルドカードと <c>re:</c> 正規表現。
    /// </summary>
    internal sealed class DmmTagExcludePatternMatcher
    {
        public const string RegexPrefix = "re:";

        public static DmmTagExcludePatternMatcher Shared { get; } = new();

        private readonly object _gate = new();
        private IReadOnlyList<CompiledPattern> _patterns = [];
        private bool _loaded;

        public sealed class ParseResult
        {
            public IReadOnlyList<string> InvalidLines { get; init; } = [];
            public int PatternCount { get; init; }
            public bool IsValid => InvalidLines.Count == 0;
        }

        private sealed class CompiledPattern(string sourceLine, Regex regex)
        {
            public string SourceLine { get; } = sourceLine;
            public Regex Regex { get; } = regex;
        }

        public void ReloadFromSettings()
        {
            ReloadFrom(Properties.Settings.Default.DmmTagExcludePatterns ?? "");
        }

        public void ReloadFrom(string multilineText)
        {
            ParseResult parsed = TryCompile(multilineText, out IReadOnlyList<CompiledPattern> patterns);
            lock (_gate)
            {
                _patterns = patterns;
                _loaded = true;
            }

            _ = parsed;
        }

        public bool IsExcluded(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            EnsureLoaded();
            string trimmed = name.Trim();
            IReadOnlyList<CompiledPattern> snapshot;
            lock (_gate)
            {
                snapshot = _patterns;
            }

            foreach (CompiledPattern pattern in snapshot)
            {
                if (pattern.Regex.IsMatch(trimmed))
                {
                    return true;
                }
            }

            return false;
        }

        public static ParseResult Validate(string multilineText)
        {
            return TryCompile(multilineText, out _);
        }

        public static string NormalizeForStorage(string multilineText)
        {
            if (string.IsNullOrWhiteSpace(multilineText))
            {
                return "";
            }

            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in SplitLines(multilineText))
            {
                string line = raw.Trim();
                if (line.Length == 0 || !seen.Add(line))
                {
                    continue;
                }

                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private void EnsureLoaded()
        {
            lock (_gate)
            {
                if (_loaded)
                {
                    return;
                }
            }

            ReloadFromSettings();
        }

        private static ParseResult TryCompile(string multilineText, out IReadOnlyList<CompiledPattern> patterns)
        {
            var compiled = new List<CompiledPattern>();
            var invalid = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in SplitLines(multilineText ?? ""))
            {
                string line = raw.Trim();
                if (line.Length == 0 || !seen.Add(line))
                {
                    continue;
                }

                if (!TryCompileLine(line, out Regex regex, out string error))
                {
                    invalid.Add(string.IsNullOrEmpty(error) ? line : $"{line} ({error})");
                    continue;
                }

                compiled.Add(new CompiledPattern(line, regex));
            }

            patterns = compiled;
            return new ParseResult
            {
                InvalidLines = invalid,
                PatternCount = compiled.Count,
            };
        }

        private static bool TryCompileLine(string line, out Regex regex, out string error)
        {
            regex = null;
            error = null;

            try
            {
                if (line.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string body = line[RegexPrefix.Length..];
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        error = "正規表現が空です";
                        return false;
                    }

                    regex = new Regex(body, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                    return true;
                }

                string globRegex = GlobToAnchoredRegex(line);
                regex = new Regex(globRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                return true;
            }
            catch (ArgumentException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// <c>*</c>→任意文字列、<c>?</c>→任意1文字。それ以外はリテラル。全体一致。
        /// </summary>
        internal static string GlobToAnchoredRegex(string pattern)
        {
            var sb = new StringBuilder("^");
            foreach (char ch in pattern)
            {
                switch (ch)
                {
                    case '*':
                        sb.Append(".*");
                        break;
                    case '?':
                        sb.Append('.');
                        break;
                    default:
                        sb.Append(Regex.Escape(ch.ToString()));
                        break;
                }
            }

            sb.Append('$');
            return sb.ToString();
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
}
