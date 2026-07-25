using System.IO;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// ファイル名から品番を抽出し、DMM ItemList 用の CID 候補を生成する。
    /// </summary>
    internal static partial class DmmCidNormalizer
    {
        // 英字+区切り+数字（任意で末尾1英字: abcd-123 / abcd-123a / abcd 123）
        [GeneratedRegex(
            @"(?<![A-Za-z0-9])(?<maker>[A-Za-z]{2,10})[-_ ]?(?<num>\d{2,6})(?<branch>[A-Za-z])?(?![A-Za-z0-9])",
            RegexOptions.CultureInvariant)]
        private static partial Regex ProductCodeRegex();

        [GeneratedRegex(@"([-_]?(cd|part|disc)\d+|[-_][a-z])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex TrailingBranchRegex();

        public sealed class ExtractResult
        {
            public string ProductCode { get; init; }
            /// <summary>ハイフンをスペースにした表記（例: abcd 123）。</summary>
            public string SpaceForm { get; init; }
            /// <summary>数字直後の枝番英字（例: a / b）。無いときは null。</summary>
            public string BranchLetter { get; init; }
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

            string maker = match.Groups["maker"].Value.ToLowerInvariant();
            string num = match.Groups["num"].Value;
            string branch = match.Groups["branch"].Success
                ? match.Groups["branch"].Value.ToLowerInvariant()
                : null;
            string hyphenForm = $"{maker}-{num}";
            string spaceForm = $"{maker} {num}";

            return new ExtractResult
            {
                ProductCode = hyphenForm,
                SpaceForm = spaceForm,
                BranchLetter = branch,
                CidCandidates = BuildCidCandidates(maker, num),
            };
        }

        public static IReadOnlyList<string> BuildCidCandidates(string makerLower, string numberDigits)
        {
            string maker = (makerLower ?? "").Trim().ToLowerInvariant();
            string num = (numberDigits ?? "").Trim();
            if (string.IsNullOrEmpty(maker) || string.IsNullOrEmpty(num))
            {
                return [];
            }

            string padded5 = num.PadLeft(5, '0');
            string padded3 = num.Length >= 3 ? num : num.PadLeft(3, '0');

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

            // 実測ベースの優先順（例: maker=abcd / num=123）
            Add("1" + maker + padded5);          // 1abcd00123
            Add(maker + padded5);                // abcd00123
            Add(maker + num);                    // abcd123 / efgh456
            Add(maker + padded3);                // abcd123 (短めパディング)
            Add(maker + "-" + num);              // abcd-123
            Add(maker + "-" + padded5);          // abcd-00123

            return candidates;
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
