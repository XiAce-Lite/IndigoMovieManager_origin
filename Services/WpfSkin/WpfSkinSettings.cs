using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services.WpfSkin
{
    /// <summary>
    /// 実行中の WPF スキンに依存する状態を保持する。
    /// </summary>
    internal static class WpfSkinSettings
    {
        /// <summary>現在の WPF スキンのサムネ生成レイアウト（skin.json の thumbnail セクション）。</summary>
        public static ThumbnailLayoutSpec CurrentThumbnailLayout { get; set; }

        /// <summary>現在スキンがジャケ写優先表示か。</summary>
        public static bool PreferJacket { get; set; }
    }
}
