using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>ジャケ救済用に組み立てた 1 URL（URL は手編集可）。</summary>
    internal sealed class DmmJacketGuessRow : INotifyPropertyChanged
    {
        private string _url;

        public string Cid { get; init; }

        /// <summary>テンプレ識別（aws-video / pics-mono など）。</summary>
        public string HostLabel { get; init; }

        public string Url
        {
            get => _url;
            set
            {
                string next = value ?? string.Empty;
                if (string.Equals(_url, next, StringComparison.Ordinal))
                {
                    return;
                }

                _url = next;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// API 未ヒット時のジャケ救済用。検索語をそのまま CID として固定 CDN テンプレへ展開する。
    /// </summary>
    internal static partial class DmmJacketUrlGuess
    {
        private static readonly (string HostLabel, string Template)[] Templates =
        [
            ("aws-video", "https://awsimgsrc.dmm.co.jp/pics_dig/digital/video/{0}/{0}pl.jpg"),
            ("pics-video", "https://pics.dmm.co.jp/digital/video/{0}/{0}pl.jpg"),
            ("aws-mono", "https://awsimgsrc.dmm.co.jp/pics_dig/mono/movie/adult/{0}/{0}pl.jpg"),
            ("pics-mono", "https://pics.dmm.co.jp/mono/movie/adult/{0}/{0}pl.jpg"),
        ];

        /// <summary>
        /// CDN パスに使える CID。英数字とアンダースコア（例: h_000abcd00123）。
        /// ハイフンや空白は不可（検索語をそのまま使う）。
        /// </summary>
        [GeneratedRegex(@"^[a-z0-9_]+$", RegexOptions.CultureInvariant)]
        private static partial Regex PathSafeCidRegex();

        /// <summary>
        /// 検索語を trim / 小文字化したうえで、そのまま 1 CID として使う。
        /// 品番正規化・接頭辞除去・候補展開はしない。
        /// </summary>
        public static IReadOnlyList<string> CollectLiteralCid(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return [];
            }

            string cid = keyword.Trim().ToLowerInvariant();
            return IsPathSafeCid(cid) ? [cid] : [];
        }

        /// <summary>
        /// CID ごとにテンプレを展開（同一 CID が並ぶので生成関係が追いやすい）。
        /// </summary>
        public static IReadOnlyList<DmmJacketGuessRow> BuildRows(IEnumerable<string> pathSafeCids)
        {
            var rows = new List<DmmJacketGuessRow>();
            if (pathSafeCids == null)
            {
                return rows;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawCid in pathSafeCids)
            {
                if (!IsPathSafeCid(rawCid))
                {
                    continue;
                }

                string cid = rawCid.Trim().ToLowerInvariant();
                foreach ((string hostLabel, string template) in Templates)
                {
                    string url = string.Format(template, cid);
                    if (!seen.Add(url))
                    {
                        continue;
                    }

                    rows.Add(new DmmJacketGuessRow
                    {
                        Cid = cid,
                        HostLabel = hostLabel,
                        Url = url,
                    });
                }
            }

            return rows;
        }

        public static IReadOnlyList<string> BuildUrls(IEnumerable<string> pathSafeCids) =>
            [.. BuildRows(pathSafeCids).Select(r => r.Url)];

        /// <summary>検索語ボックスの内容だけから推定 URL を生成する。</summary>
        public static IReadOnlyList<DmmJacketGuessRow> BuildRowsFromKeyword(string keyword) =>
            BuildRows(CollectLiteralCid(keyword));

        public static IReadOnlyList<string> BuildUrlsFromKeyword(string keyword) =>
            BuildUrls(CollectLiteralCid(keyword));

        internal static bool IsPathSafeCid(string cid) =>
            !string.IsNullOrWhiteSpace(cid) && PathSafeCidRegex().IsMatch(cid.Trim().ToLowerInvariant());
    }
}
