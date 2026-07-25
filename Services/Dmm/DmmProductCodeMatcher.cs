using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// 要求品番と DMM 候補の maker+番号一致判定（ゼロパディング差は同一視）。
    /// </summary>
    internal static partial class DmmProductCodeMatcher
    {
        // DMM content_id 形式: 任意の先頭1 + メーカー + 数字（例: 1abcd00107 / abcd00107）
        [GeneratedRegex(
            @"^1?(?<maker>[A-Za-z]{2,10})(?<num>\d{2,6})(?<branch>[A-Za-z])?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ContentIdRegex();

        public static bool ItemMatchesProductCode(DmmItemDto item, string productCode)
        {
            if (item == null || !TryGetMakerNumber(productCode, out string wantMaker, out string wantNum))
            {
                return false;
            }

            string wantNumKey = NormalizeNumberKey(wantNum);

            foreach (string cid in DmmCidNormalizer.BuildCidCandidates(wantMaker, wantNum))
            {
                if (EqualsIgnoreCase(item.ContentId, cid) || EqualsIgnoreCase(item.ProductId, cid))
                {
                    return true;
                }
            }

            string hyphen = $"{wantMaker}-{wantNum}";
            string space = $"{wantMaker} {wantNum}";
            if (EqualsIgnoreCase(item.ProductId, hyphen)
                || EqualsIgnoreCase(item.ProductId, space)
                || EqualsIgnoreCase(item.ContentId, hyphen))
            {
                return true;
            }

            foreach (string source in new[] { item.ProductId, item.ContentId, item.Title })
            {
                if (!TryGetMakerNumber(source, out string gotMaker, out string gotNum))
                {
                    continue;
                }

                if (!string.Equals(gotMaker, wantMaker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(NormalizeNumberKey(gotNum), wantNumKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetMakerNumber(string value, out string maker, out string number)
        {
            maker = null;
            number = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(value);
            if (extracted.HasProductCode)
            {
                return SplitHyphenProductCode(extracted.ProductCode, out maker, out number);
            }

            Match match = ContentIdRegex().Match(value.Trim());
            if (!match.Success)
            {
                return false;
            }

            maker = match.Groups["maker"].Value.ToLowerInvariant();
            number = match.Groups["num"].Value;
            return true;
        }

        public static string NormalizeNumberKey(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
            {
                return "0";
            }

            string trimmed = digits.Trim().TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        private static bool SplitHyphenProductCode(string productCode, out string maker, out string number)
        {
            maker = null;
            number = null;
            if (string.IsNullOrWhiteSpace(productCode))
            {
                return false;
            }

            int dash = productCode.IndexOf('-');
            if (dash <= 0 || dash >= productCode.Length - 1)
            {
                return false;
            }

            maker = productCode[..dash].ToLowerInvariant();
            number = productCode[(dash + 1)..];
            return maker.Length > 0 && number.Length > 0;
        }

        private static bool EqualsIgnoreCase(string a, string b) =>
            !string.IsNullOrEmpty(a)
            && !string.IsNullOrEmpty(b)
            && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
