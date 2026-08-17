using System.IO;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// ファイル名から品番を抽出し、DMM ItemList 用の CID 候補を生成する。
    /// </summary>
    internal static partial class DmmCidNormalizer
    {
        // 任意の配信コード接頭辞(1〜4桁) + 英字メーカー + 区切り + 数字
        // 例: abcd-123 / abcd-123a / abcd 123 / 529abcd-123
        [GeneratedRegex(
            @"(?<![A-Za-z0-9])(?<prefix>\d{1,4})?(?<maker>[A-Za-z]{2,10})[-_ ]?(?<num>\d{2,6})(?<branch>[A-Za-z])?(?![A-Za-z0-9])",
            RegexOptions.CultureInvariant)]
        private static partial Regex ProductCodeRegex();

        [GeneratedRegex(@"([-_]?(cd|part|disc)\d+|[-_][a-z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TrailingBranchRegex();

        // 直 content_id: 任意の h_ + 配信コード(1〜4桁) + メーカー + 数字
        // 例: 1abcd000030 / 529abcd00123 / h_000abcd00123 / h_491abcd00022
        [GeneratedRegex(
            @"^(?<h>h_)?(?<prefix>\d{1,4})?(?<maker>[A-Za-z]{2,10})(?<num>\d{2,6})(?<branch>[A-Za-z])?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DirectContentIdRegex();

        // FANZA / DMM 商品 URL の id= / cid=（クエリ・パス断片）
        [GeneratedRegex(
            @"[?&#/](?:id|cid)=(?<cid>[A-Za-z0-9_]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex UrlContentIdRegex();

        [GeneratedRegex(@"^[a-z0-9_]+$", RegexOptions.CultureInvariant)]
        private static partial Regex PathSafeCidRegex();

        public sealed class ExtractResult
        {
            public string ProductCode { get; init; }
            /// <summary>ハイフンをスペースにした表記（例: abcd 123）。</summary>
            public string SpaceForm { get; init; }
            /// <summary>先頭ゼロを落としたハイフン品番（例: abcd-022 → abcd-22）。同一なら null。</summary>
            public string StrippedProductCode { get; init; }
            /// <summary>先頭ゼロ落としのスペース表記。同一なら null。</summary>
            public string StrippedSpaceForm { get; init; }
            /// <summary>数字直後の枝番英字（例: a / b）。無いときは null。</summary>
            public string BranchLetter { get; init; }
            /// <summary>ファイル名先頭などに付く配信コード（例: 529）。無いときは null。</summary>
            public string ChannelPrefix { get; init; }
            /// <summary>入力が h_ 付き content_id だったとき true。</summary>
            public bool HasHUnderscorePrefix { get; init; }
            /// <summary>検索入力の原文 CID（URL 抽出・直入力）。無いときは null。</summary>
            public string LiteralCid { get; init; }
            public IReadOnlyList<string> CidCandidates { get; init; }

            public bool HasProductCode => !string.IsNullOrEmpty(ProductCode);

            public string ProductCodeWithBranch =>
                string.IsNullOrEmpty(BranchLetter) ? ProductCode : ProductCode + BranchLetter;
        }

        public static ExtractResult ExtractFromFileName(string movieName)
        {
            string body = NormalizeBody(movieName);
            if (string.IsNullOrEmpty(body))
            {
                return new ExtractResult { ProductCode = null, CidCandidates = [] };
            }

            // ファイル名全体が直 content_id（h_ 付き含む）なら優先
            Match direct = DirectContentIdRegex().Match(body);
            if (direct.Success && LooksLikeCompactContentId(body, direct))
            {
                return BuildExtractResult(direct, normalizeNumber: true, literalCid: body.ToLowerInvariant());
            }

            string stripped = TrailingBranchRegex().Replace(body, "");
            Match match = ProductCodeRegex().Match(stripped);
            if (!match.Success)
            {
                match = ProductCodeRegex().Match(body);
            }

            if (!match.Success)
            {
                return new ExtractResult { ProductCode = null, CidCandidates = [] };
            }

            return BuildExtractResult(match, normalizeNumber: false);
        }

        public static ExtractResult ExtractFromSearchInput(string searchInput)
        {
            if (string.IsNullOrWhiteSpace(searchInput))
            {
                return new ExtractResult { ProductCode = null, CidCandidates = [] };
            }

            string trimmed = searchInput.Trim();
            if (TryExtractCidFromUrlOrQuery(trimmed, out string urlCid))
            {
                return ExtractFromSearchInput(urlCid);
            }

            string body = NormalizeBody(trimmed);
            if (string.IsNullOrEmpty(body))
            {
                return new ExtractResult { ProductCode = null, CidCandidates = [] };
            }

            string lower = body.ToLowerInvariant();

            // パス安全な直 CID（h_491abcd00022 等）は原文を最優先候補にする
            Match direct = DirectContentIdRegex().Match(lower);
            if (direct.Success)
            {
                string literal = IsPathSafeCid(lower) ? lower : null;
                return BuildExtractResult(direct, normalizeNumber: true, literalCid: literal);
            }

            if (IsPathSafeCid(lower))
            {
                // 正規表現に乗らないが CDN 用 CID として使える入力
                return new ExtractResult
                {
                    ProductCode = null,
                    CidCandidates = [lower],
                    LiteralCid = lower,
                };
            }

            return ExtractFromFileName(trimmed);
        }

        /// <summary>
        /// 自動／手動で追加試行するキーワード（入力そのものと ProductCode 以外）。
        /// スペース表記・ゼロ落とし・5桁ゼロ埋めの順。
        /// </summary>
        public static IReadOnlyList<string> BuildExtraKeywordVariants(ExtractResult extracted)
        {
            if (extracted == null || !extracted.HasProductCode)
            {
                return [];
            }

            var list = new List<string>();
            void Add(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                string trimmed = value.Trim();
                if (string.Equals(trimmed, extracted.ProductCode, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!list.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(trimmed);
                }
            }

            Add(extracted.SpaceForm);
            Add(extracted.StrippedProductCode);
            Add(extracted.StrippedSpaceForm);

            int dash = extracted.ProductCode.IndexOf('-');
            if (dash > 0 && dash < extracted.ProductCode.Length - 1)
            {
                string maker = extracted.ProductCode[..dash];
                string number = extracted.ProductCode[(dash + 1)..];
                Add(maker + number.PadLeft(5, '0'));
            }

            return list;
        }

        public static IReadOnlyList<string> BuildCidCandidates(
            string makerLower,
            string numberDigits,
            string channelPrefix = null,
            bool includeHUnderscore = false)
        {
            string maker = (makerLower ?? "").Trim().ToLowerInvariant();
            string num = (numberDigits ?? "").Trim();
            if (string.IsNullOrEmpty(maker) || string.IsNullOrEmpty(num))
            {
                return [];
            }

            string padded5 = num.PadLeft(5, '0');
            string padded6 = num.PadLeft(6, '0');
            string padded3 = num.Length >= 3 ? num : num.PadLeft(3, '0');
            string prefix = (channelPrefix ?? "").Trim();

            var candidates = new List<string>();
            void Add(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                if (!candidates.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(value);
                }
            }

            // h_ + 配信コード付き（入力に h_ があった／明示指定時）
            if (includeHUnderscore && prefix.Length > 0)
            {
                Add("h_" + prefix + maker + padded5);
                Add("h_" + prefix + maker + padded6);
                Add("h_" + prefix + maker + num);
            }

            // ファイルから取れた配信コード付きを優先（例: 529abcd00123）
            if (prefix.Length > 0)
            {
                Add(prefix + maker + padded5);
                Add(prefix + maker + padded6);
                Add(prefix + maker + num);
            }

            // 実測ベースの優先順（例: maker=abcd / num=123）
            Add("1" + maker + padded5);          // 1abcd00123
            Add(maker + padded5);                // abcd00123
            Add("1" + maker + padded6);          // 1abcd000123
            Add(maker + padded6);                // abcd000123
            Add(maker + num);                    // abcd123 / efgh456
            Add(maker + padded3);                // abcd123 (短めパディング)
            Add(maker + "-" + num);              // abcd-123
            Add(maker + "-" + padded5);          // abcd-00123
            Add(maker + "-" + padded6);          // abcd-000123

            return candidates;
        }

        /// <summary>先頭ゼロを落とした数字（再パディングしない）。例: 022 → 22。</summary>
        public static string StripLeadingZeros(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits))
            {
                return "0";
            }

            string trimmed = digits.Trim().TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        internal static bool IsPathSafeCid(string cid) =>
            !string.IsNullOrWhiteSpace(cid) && PathSafeCidRegex().IsMatch(cid.Trim().ToLowerInvariant());

        internal static bool TryExtractCidFromUrlOrQuery(string input, out string cid)
        {
            cid = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();
            bool looksLikeUrl =
                trimmed.Contains("://", StringComparison.Ordinal)
                || trimmed.Contains("dmm.co.jp", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("dmm.com", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith('?')
                || trimmed.Contains("id=", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("cid=", StringComparison.OrdinalIgnoreCase);

            if (!looksLikeUrl)
            {
                return false;
            }

            Match match = UrlContentIdRegex().Match(trimmed);
            if (!match.Success)
            {
                // クエリだけの断片: id=h_000abcd00123
                if (trimmed.StartsWith("id=", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("cid=", StringComparison.OrdinalIgnoreCase))
                {
                    int eq = trimmed.IndexOf('=');
                    if (eq > 0 && eq < trimmed.Length - 1)
                    {
                        string raw = trimmed[(eq + 1)..];
                        int amp = raw.IndexOfAny(['&', '#']);
                        if (amp >= 0)
                        {
                            raw = raw[..amp];
                        }

                        raw = Uri.UnescapeDataString(raw).Trim().ToLowerInvariant();
                        if (IsPathSafeCid(raw))
                        {
                            cid = raw;
                            return true;
                        }
                    }
                }

                return false;
            }

            string extracted = match.Groups["cid"].Value.Trim().ToLowerInvariant();
            if (!IsPathSafeCid(extracted))
            {
                return false;
            }

            cid = extracted;
            return true;
        }

        private static ExtractResult BuildExtractResult(
            Match match,
            bool normalizeNumber,
            string literalCid = null)
        {
            string maker = match.Groups["maker"].Value.ToLowerInvariant();
            string num = match.Groups["num"].Value;
            string branch = match.Groups["branch"].Success
                ? match.Groups["branch"].Value.ToLowerInvariant()
                : null;
            string prefix = match.Groups["prefix"].Success
                ? match.Groups["prefix"].Value
                : null;
            bool hasH = match.Groups["h"].Success
                && !string.IsNullOrEmpty(match.Groups["h"].Value);

            string productNumber = normalizeNumber ? NormalizeProductNumber(num) : num;
            string hyphenForm = $"{maker}-{productNumber}";
            string spaceForm = $"{maker} {productNumber}";

            string strippedNum = StripLeadingZeros(productNumber);
            string strippedHyphen = null;
            string strippedSpace = null;
            if (!string.Equals(strippedNum, productNumber, StringComparison.Ordinal))
            {
                strippedHyphen = $"{maker}-{strippedNum}";
                strippedSpace = $"{maker} {strippedNum}";
            }

            var candidates = new List<string>();
            void AddCid(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                if (!candidates.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(value);
                }
            }

            if (!string.IsNullOrEmpty(literalCid))
            {
                AddCid(literalCid.Trim().ToLowerInvariant());
            }

            foreach (string c in BuildCidCandidates(maker, num, prefix, includeHUnderscore: hasH))
            {
                AddCid(c);
            }

            // 先頭ゼロ落とし番号でもう一度（abcd022 と abcd22 など）
            string numStripped = StripLeadingZeros(num);
            if (!string.Equals(numStripped, num, StringComparison.Ordinal))
            {
                foreach (string c in BuildCidCandidates(maker, numStripped, prefix, includeHUnderscore: hasH))
                {
                    AddCid(c);
                }
            }

            return new ExtractResult
            {
                ProductCode = hyphenForm,
                SpaceForm = spaceForm,
                StrippedProductCode = strippedHyphen,
                StrippedSpaceForm = strippedSpace,
                BranchLetter = branch,
                ChannelPrefix = prefix,
                HasHUnderscorePrefix = hasH,
                LiteralCid = literalCid,
                CidCandidates = candidates,
            };
        }

        /// <summary>
        /// ハイフン無しコンパクト形かどうか。ファイル名の abcd-123 を誤って直 CID 扱いしない。
        /// </summary>
        private static bool LooksLikeCompactContentId(string body, Match direct)
        {
            if (!direct.Success || string.IsNullOrEmpty(body))
            {
                return false;
            }

            if (body.Contains('-') || body.Contains(' '))
            {
                return false;
            }

            int underscore = body.IndexOf('_');
            if (underscore < 0)
            {
                return true;
            }

            // アンダースコアは h_ 接頭辞のみ許可（例: h_000abcd00123）
            return body.StartsWith("h_", StringComparison.OrdinalIgnoreCase)
                && body.IndexOf('_', 2) < 0;
        }

        private static string NormalizeProductNumber(string digits)
        {
            string normalized = (digits ?? string.Empty).Trim().TrimStart('0');
            if (normalized.Length == 0)
            {
                normalized = "0";
            }

            return normalized.Length >= 3 ? normalized : normalized.PadLeft(3, '0');
        }

        private static string NormalizeBody(string movieName)
        {
            if (string.IsNullOrWhiteSpace(movieName))
            {
                return "";
            }

            string name = movieName.Trim();
            string ext = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
            {
                name = Path.GetFileNameWithoutExtension(name);
            }

            return name.Trim();
        }
    }
}
