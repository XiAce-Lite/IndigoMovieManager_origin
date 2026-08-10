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

        // 先頭の配信コード(1〜4桁)を許容（従来の任意 1 も含む）
        [GeneratedRegex(
            @"^(?<prefix>\d{1,4})?(?<maker>[A-Za-z]{2,10})(?<num>\d{2,6})(?<branch>[A-Za-z])?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex DirectContentIdRegex();

        public sealed class ExtractResult
        {
            public string ProductCode { get; init; }
            /// <summary>ハイフンをスペースにした表記（例: abcd 123）。</summary>
            public string SpaceForm { get; init; }
            /// <summary>数字直後の枝番英字（例: a / b）。無いときは null。</summary>
            public string BranchLetter { get; init; }
            /// <summary>ファイル名先頭などに付く配信コード（例: 529）。無いときは null。</summary>
            public string ChannelPrefix { get; init; }
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
            string body = NormalizeBody(searchInput);
            if (string.IsNullOrEmpty(body))
            {
                return new ExtractResult { ProductCode = null, CidCandidates = [] };
            }

            // コンパクト CID（1abcd000030 / 529abcd00123）は番号を正規化して返す
            Match direct = DirectContentIdRegex().Match(body);
            if (direct.Success)
            {
                return BuildExtractResult(direct, normalizeNumber: true);
            }

            return ExtractFromFileName(searchInput);
        }

        public static IReadOnlyList<string> BuildCidCandidates(
            string makerLower,
            string numberDigits,
            string channelPrefix = null)
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

        private static ExtractResult BuildExtractResult(Match match, bool normalizeNumber)
        {
            string maker = match.Groups["maker"].Value.ToLowerInvariant();
            string num = match.Groups["num"].Value;
            string branch = match.Groups["branch"].Success
                ? match.Groups["branch"].Value.ToLowerInvariant()
                : null;
            string prefix = match.Groups["prefix"].Success
                ? match.Groups["prefix"].Value
                : null;

            string productNumber = normalizeNumber ? NormalizeProductNumber(num) : num;
            string hyphenForm = $"{maker}-{productNumber}";
            string spaceForm = $"{maker} {productNumber}";

            return new ExtractResult
            {
                ProductCode = hyphenForm,
                SpaceForm = spaceForm,
                BranchLetter = branch,
                ChannelPrefix = prefix,
                CidCandidates = BuildCidCandidates(maker, num, prefix),
            };
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
