namespace IndigoMovieManager.Services.WpfSkin.Design
{
    internal enum WpfSkinFieldKind
    {
        Text,
        Path,
        Tags,
        Thumbnail,
    }

    internal sealed class WpfSkinFieldDescriptor
    {
        public string Id { get; init; }
        public string DisplayName { get; init; }
        public WpfSkinFieldKind Kind { get; init; }
        public string DefaultStyleKey { get; init; }
    }

    /// <summary>
    /// 簡易エディタ用の DB 項目カタログ。配置一意の単位でもある。
    /// </summary>
    internal static class WpfSkinFieldCatalog
    {
        /// <summary>旧互換: source 無し thumbnail ノードのキー。</summary>
        public const string ThumbnailId = "thumbnail";

        public const string ThumbnailLocalId = "thumbnail:local";
        public const string ThumbnailJacketId = "thumbnail:comment1";
        public const string TagsId = "tags";

        public static readonly IReadOnlyList<WpfSkinFieldDescriptor> All =
        [
            new() { Id = ThumbnailLocalId, DisplayName = "サムネイル（ローカル）", Kind = WpfSkinFieldKind.Thumbnail, DefaultStyleKey = "" },
            new() { Id = ThumbnailJacketId, DisplayName = "ジャケ写（Comment1）", Kind = WpfSkinFieldKind.Thumbnail, DefaultStyleKey = "" },
            new() { Id = "title", DisplayName = "タイトル（ファイル名）", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "title" },
            new() { Id = "metatitle", DisplayName = "メタタイトル", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "title" },
            new() { Id = "body", DisplayName = "ファイル名（拡張子なし）", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "artist", DisplayName = "メーカー / Artist", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "genre", DisplayName = "ジャンル", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "album", DisplayName = "アルバム", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = TagsId, DisplayName = "タグ", Kind = WpfSkinFieldKind.Tags, DefaultStyleKey = "" },
            new() { Id = "path", DisplayName = "フルパス", Kind = WpfSkinFieldKind.Path, DefaultStyleKey = "path" },
            new() { Id = "dir", DisplayName = "親フォルダ", Kind = WpfSkinFieldKind.Path, DefaultStyleKey = "path" },
            new() { Id = "drive", DisplayName = "ドライブ", Kind = WpfSkinFieldKind.Path, DefaultStyleKey = "path" },
            new() { Id = "length", DisplayName = "再生時間", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "size", DisplayName = "ファイルサイズ", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "score", DisplayName = "スコア", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "viewcount", DisplayName = "視聴回数", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "filedate", DisplayName = "ファイル日時", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "registdate", DisplayName = "登録日時", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "lastdate", DisplayName = "最終視聴", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "container", DisplayName = "コンテナ", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "video", DisplayName = "映像", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "audio", DisplayName = "音声", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "ext", DisplayName = "拡張子", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "comment1", DisplayName = "Comment1（URL）", Kind = WpfSkinFieldKind.Path, DefaultStyleKey = "path" },
            new() { Id = "comment2", DisplayName = "Comment2", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
            new() { Id = "comment3", DisplayName = "Comment3", Kind = WpfSkinFieldKind.Text, DefaultStyleKey = "meta" },
        ];

        private static readonly Dictionary<string, WpfSkinFieldDescriptor> ById =
            All.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        public static bool TryGet(string id, out WpfSkinFieldDescriptor descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return ById.TryGetValue(id.Trim(), out descriptor);
        }

        public static WpfSkinFieldDescriptor GetRequired(string id) =>
            TryGet(id, out WpfSkinFieldDescriptor d)
                ? d
                : throw new ArgumentException($"Unknown field id: {id}", nameof(id));

        public static bool IsPathField(string field) =>
            TryGet(field, out WpfSkinFieldDescriptor d) && d.Kind == WpfSkinFieldKind.Path;

        public static bool IsThumbnailFieldId(string fieldId) =>
            string.Equals(fieldId, ThumbnailLocalId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fieldId, ThumbnailJacketId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fieldId, ThumbnailId, StringComparison.OrdinalIgnoreCase);

        public static string GetDefaultStyleKey(string fieldId) =>
            TryGet(fieldId, out WpfSkinFieldDescriptor d)
                ? d.DefaultStyleKey ?? ""
                : "";

        /// <summary>
        /// layout 上の一意キー。静的ラベルや container は null。
        /// source 無し thumbnail は <see cref="ThumbnailId"/>（収集時に local/jacket 両方使用扱いに展開）。
        /// </summary>
        public static string ResolveUniqueKey(WpfSkinNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(node.Panel)
                && (string.Equals(node.Panel, "stack", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Panel, "grid", StringComparison.OrdinalIgnoreCase))
                && string.IsNullOrWhiteSpace(node.Type))
            {
                return null;
            }

            string type = node.Type?.Trim().ToLowerInvariant() ?? "";
            if (type == "thumbnail")
            {
                string src = node.Source?.Trim().ToLowerInvariant() ?? "";
                if (string.Equals(src, "local", StringComparison.OrdinalIgnoreCase))
                {
                    return ThumbnailLocalId;
                }

                if (string.Equals(src, "comment1", StringComparison.OrdinalIgnoreCase))
                {
                    return ThumbnailJacketId;
                }

                // source 無し = ローカル兼用枠（preferJacket 時は同枠でジャケ差し替え）。
                // パレットでは local のみ使用済み（ジャケ写枠は別途配置可＝同居へ）。
                return ThumbnailId;
            }

            if (type == "tags")
            {
                return TagsId;
            }

            if (type is "" or "text")
            {
                if (string.IsNullOrWhiteSpace(node.Field))
                {
                    return null;
                }

                return node.Field.Trim().ToLowerInvariant();
            }

            return null;
        }

        /// <summary>
        /// パレット用: source 無し thumbnail は local のみ使用済みにする。
        /// （ジャケ写は未配置のまま＝同居用に追加できる）
        /// </summary>
        public static void AddExpandedUsedKeys(ISet<string> used, string uniqueKey)
        {
            if (used == null || string.IsNullOrEmpty(uniqueKey))
            {
                return;
            }

            if (string.Equals(uniqueKey, ThumbnailId, StringComparison.OrdinalIgnoreCase))
            {
                used.Add(ThumbnailLocalId);
                used.Add(ThumbnailId);
                return;
            }

            used.Add(uniqueKey);
        }

        public static HashSet<string> CollectUsedFieldIds(WpfSkinNode root)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectUsedFieldIds(root, used);
            return used;
        }

        public static void CollectUsedFieldIds(WpfSkinNode node, ISet<string> used)
        {
            if (node == null || used == null)
            {
                return;
            }

            string key = ResolveUniqueKey(node);
            AddExpandedUsedKeys(used, key);

            foreach (WpfSkinNode child in node.Children ?? [])
            {
                CollectUsedFieldIds(child, used);
            }
        }

        public static IEnumerable<WpfSkinFieldDescriptor> UnusedFields(WpfSkinNode root)
        {
            HashSet<string> used = CollectUsedFieldIds(root);
            return All.Where(f => !used.Contains(f.Id));
        }
    }
}
