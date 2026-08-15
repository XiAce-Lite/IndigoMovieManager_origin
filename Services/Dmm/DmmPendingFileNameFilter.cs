using System.Text;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// DMM 未確定候補一覧のファイル名絞り込み。
    /// 引用符で囲むと直接部分一致、それ以外は品番揺れを含む曖昧一致。
    /// </summary>
    internal static class DmmPendingFileNameFilter
    {
        public static bool IsBroadQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            string text = query.Trim();
            return TryGetQuotedExact(text, out string exact) && exact.Length == 0;
        }

        public static bool Matches(string fileName, string query)
        {
            if (IsBroadQuery(query))
            {
                return true;
            }

            string text = query.Trim();
            string name = fileName ?? "";

            if (TryGetQuotedExact(text, out string exact))
            {
                return name.Contains(exact, StringComparison.CurrentCultureIgnoreCase);
            }

            return MatchesFuzzy(name, text);
        }

        private static bool TryGetQuotedExact(string text, out string exact)
        {
            exact = null;
            if (text.Length < 2)
            {
                return false;
            }

            if ((text.StartsWith('"') && text.EndsWith('"'))
                || (text.StartsWith('\'') && text.EndsWith('\'')))
            {
                exact = text[1..^1];
                return true;
            }

            return false;
        }

        private static bool MatchesFuzzy(string fileName, string query)
        {
            if (MatchesProductCode(fileName, query))
            {
                return true;
            }

            string compactQuery = Compact(query);
            return compactQuery.Length > 0
                && Compact(fileName).Contains(compactQuery, StringComparison.Ordinal);
        }

        private static bool MatchesProductCode(string fileName, string query)
        {
            if (!DmmProductCodeMatcher.TryGetMakerNumber(query, out string wantMaker, out string wantNum)
                || !DmmProductCodeMatcher.TryGetMakerNumber(fileName, out string gotMaker, out string gotNum))
            {
                return false;
            }

            if (!string.Equals(wantMaker, gotMaker, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return string.Equals(
                DmmProductCodeMatcher.NormalizeNumberKey(wantNum),
                DmmProductCodeMatcher.NormalizeNumberKey(gotNum),
                StringComparison.Ordinal);
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var builder = new StringBuilder(value.Length);
            var digits = new StringBuilder();

            void FlushDigits()
            {
                if (digits.Length == 0)
                {
                    return;
                }

                string normalized = digits.ToString().TrimStart('0');
                builder.Append(normalized.Length == 0 ? "0" : normalized);
                digits.Clear();
            }

            foreach (char c in value.ToLowerInvariant())
            {
                if (c is '-' or '_' or ' ' or '.')
                {
                    FlushDigits();
                    continue;
                }

                if (char.IsAsciiDigit(c))
                {
                    digits.Append(c);
                    continue;
                }

                FlushDigits();
                builder.Append(c);
            }

            FlushDigits();
            return builder.ToString();
        }
    }
}
