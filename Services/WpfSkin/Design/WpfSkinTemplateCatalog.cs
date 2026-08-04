namespace IndigoMovieManager.Services.WpfSkin.Design
{
    /// <summary>新規スキン作成時に提示するテンプレート一覧。</summary>
    internal static class WpfSkinTemplateCatalog
    {
        public sealed record Entry(string FolderName, string DisplayName, string Description);

        /// <summary>存在するスキンだけ返す（バンドル欠落時はスキップ）。</summary>
        public static IReadOnlyList<Entry> Available()
        {
            var result = new List<Entry>();
            foreach (Entry entry in All)
            {
                if (WpfSkinStorage.FolderExists(entry.FolderName)
                    || string.Equals(entry.FolderName, WpfSkinLoader.DefaultSkinName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry);
                }
            }

            if (result.Count == 0)
            {
                result.Add(new Entry(
                    WpfSkinLoader.DefaultSkinName,
                    "CardLarge（標準）",
                    "大きめカード。タイトル・メタ・サムネの基本レイアウト。"));
            }

            return result;
        }

        private static readonly Entry[] All =
        [
            new("CardLarge", "CardLarge（標準）", "大きめカード。タイトル・メタ・サムネの基本レイアウト。"),
            new("BigInfo", "BigInfo", "情報量多め。テキストを厚めに載せたカード。"),
            new("CenterThumb", "CenterThumb", "サムネ中心のコンパクト配置。"),
            new("WideGridInfo", "WideGridInfo", "横長 grid で情報を並べるレイアウト。"),
            new("JacketInfo", "JacketInfo", "ジャケ写優先の情報カード。"),
            new("JacketInfo3x2", "JacketInfo3x2", "3×2 サムネ格子＋情報。"),
            new("JacketLocalSide", "JacketLocalSide", "左ジャケ＋右5×2格子＋ファイル名/タグ。"),
            new("DarkModeSample", "DarkModeSample", "暗い背景向けのサンプル配色。"),
            new("DefaultList", "DefaultList（リスト）", "list 型。ヘッダー行付きの一覧向け。"),
            new("DefaultSmall", "DefaultSmall", "小さめカードの保護テンプレ。"),
            new("DefaultGrid", "DefaultGrid", "grid 中心の保護テンプレ。"),
        ];
    }
}
