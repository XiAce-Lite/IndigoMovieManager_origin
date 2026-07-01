namespace IndigoMovieManager.Services
{
    internal static class SkinTabIndexHelper
    {
        public const int PhysicalThumbTabCount = 5;

        /// <summary>WPF ネイティブスキン（旧 tab 5）。フェーズ B 以降はレイアウトキーが正規。</summary>
        public const int WpfSkinTabIndex = 5;

        /// <summary>キュー互換用の WPF スロット番号。</summary>
        public const int WpfSkinThumbnailSlotIndex = 5;

        /// <summary>WhiteBrowser 互換スキン（旧 tab 6）。</summary>
        public const int WbSkinTabIndex = 6;

        public static bool IsWebSkinTab(int tabIndex) =>
            tabIndex == WbSkinTabIndex;

        public static bool IsWpfSkinTab(int tabIndex) =>
            tabIndex == WpfSkinTabIndex;
    }
}
