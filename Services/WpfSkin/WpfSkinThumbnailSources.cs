using System.Collections.Generic;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>thumbnail.sources の1要素。kind は local / comment1。</summary>
    public sealed class WpfSkinThumbnailSource
    {
        public string Kind { get; set; } = "";
    }

    /// <summary>sources の正規化と実行時の有効判定。</summary>
    internal static class WpfSkinThumbnailSources
    {
        public const string KindLocal = "local";
        public const string KindComment1 = "comment1";

        public const string JacketPlaySlotTag = "WpfSkinJacketPlayStart";

        /// <summary>JacketInfo 系のジャケ／フォールバック枠（幅）。</summary>
        public const double JacketInfoFallbackWidth = 360;

        /// <summary>JacketInfo 系のジャケ無し時の枠高。</summary>
        public const double JacketInfoFallbackHeight = 203;

        /// <summary>DefaultBig10 の 5×2 表示幅（120×5）。</summary>
        public const double DefaultBig10DisplayWidth = 600;

        /// <summary>DefaultBig10 の 5×2 表示高（90×2）。</summary>
        public const double DefaultBig10DisplayHeight = 180;

        /// <summary>
        /// 許可 kind のみ・先勝ち・最大2。JSON 上の Sources は変更しない（戻り値のみ）。
        /// </summary>
        public static IReadOnlyList<string> Normalize(IEnumerable<WpfSkinThumbnailSource> sources)
        {
            if (sources == null)
            {
                return [];
            }

            var result = new List<string>(2);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WpfSkinThumbnailSource src in sources)
            {
                string kind = src?.Kind?.Trim().ToLowerInvariant() ?? "";
                if (kind is not (KindLocal or KindComment1))
                {
                    continue;
                }

                if (!seen.Add(kind))
                {
                    continue;
                }

                result.Add(kind);
                if (result.Count >= 2)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// card かつ有効 sources があるとき true。list では描画に使わない（Sources 自体は保持）。
        /// </summary>
        public static bool TryGetRenderKinds(WpfSkinDefinition def, out IReadOnlyList<string> kinds)
        {
            kinds = Normalize(def?.Thumbnail?.Sources);
            if (def == null || def.IsList || kinds.Count == 0)
            {
                kinds = [];
                return false;
            }

            return true;
        }

        /// <summary>sources モード時は preferJacket を無視する。</summary>
        public static bool ShouldSuppressPreferJacket(WpfSkinDefinition def) =>
            TryGetRenderKinds(def, out _);

        public static List<WpfSkinThumbnailSource> CreateDefaultCoexist() =>
        [
            new() { Kind = KindComment1 },
            new() { Kind = KindLocal },
        ];

        public static bool IsDefaultCoexist(IEnumerable<WpfSkinThumbnailSource> sources)
        {
            IReadOnlyList<string> kinds = Normalize(sources);
            return kinds.Count == 2
                && kinds[0] == KindComment1
                && kinds[1] == KindLocal;
        }

        /// <summary>
        /// レイアウト上の thumbnail から Sources を同期する。
        /// source 無し（兼用枠）だけのときは Sources を触らない。
        /// 明示 source と兼用枠が混在するときは兼用枠を local に昇格して同居 Sources にする。
        /// 明示 source も兼用枠も無いときは Sources をクリアする。
        /// </summary>
        public static void SyncSourcesFromLayout(WpfSkinDefinition def, WpfSkinNode layoutRoot)
        {
            if (def == null)
            {
                return;
            }

            var found = new List<string>();
            bool hasPlainThumbnail = false;
            CollectExplicitSources(layoutRoot, found, ref hasPlainThumbnail);
            def.Thumbnail ??= new WpfSkinThumbnail();

            if (found.Count == 0)
            {
                if (!hasPlainThumbnail)
                {
                    def.Thumbnail.Sources = null;
                }

                return;
            }

            // BigInfo 等の兼用枠 + ジャケ写追加 → 兼用枠を local として同居に乗せる
            if (hasPlainThumbnail && !found.Contains(KindLocal))
            {
                found.Add(KindLocal);
                PromotePlainThumbnailsToLocal(layoutRoot);
            }

            // comment1 → local の順（編集 UI の同居既定に合わせる）
            var ordered = new List<WpfSkinThumbnailSource>();
            if (found.Contains(KindComment1))
            {
                ordered.Add(new WpfSkinThumbnailSource { Kind = KindComment1 });
            }

            if (found.Contains(KindLocal))
            {
                ordered.Add(new WpfSkinThumbnailSource { Kind = KindLocal });
            }

            def.Thumbnail.Sources = ordered;
            if (ordered.Count > 1)
            {
                def.Thumbnail.PreferJacket = false;
            }
        }

        private static void CollectExplicitSources(
            WpfSkinNode node,
            List<string> found,
            ref bool hasPlainThumbnail)
        {
            if (node == null)
            {
                return;
            }

            if (string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase))
            {
                string src = node.Source?.Trim().ToLowerInvariant() ?? "";
                if (src is KindLocal or KindComment1)
                {
                    if (!found.Contains(src))
                    {
                        found.Add(src);
                    }
                }
                else
                {
                    hasPlainThumbnail = true;
                }
            }

            foreach (WpfSkinNode child in node.Children ?? [])
            {
                CollectExplicitSources(child, found, ref hasPlainThumbnail);
            }
        }

        /// <summary>source 無し thumbnail に source=local を付け、パレット／描画と一致させる。</summary>
        private static void PromotePlainThumbnailsToLocal(WpfSkinNode node)
        {
            if (node == null)
            {
                return;
            }

            if (string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(node.Source))
            {
                node.Source = KindLocal;
            }

            foreach (WpfSkinNode child in node.Children ?? [])
            {
                PromotePlainThumbnailsToLocal(child);
            }
        }
    }
}
