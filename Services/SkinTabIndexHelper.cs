namespace IndigoMovieManager.Services
{
    internal static class SkinTabIndexHelper
    {
        public const int PhysicalThumbTabCount = 5;

        /// <summary>WPF ネイティブスキンタブ（旧 Small(Web) の枠を流用）。</summary>
        public const int WpfSkinTabIndex = 5;

        /// <summary>WPF スキン用サムネ生成スロット（キュー・パス解決の論理番号）。</summary>
        public const int WpfSkinThumbnailSlotIndex = 5;

        /// <summary>WhiteBrowser 互換スキンタブ（WebView2 ホスト）。</summary>
        public const int WbSkinTabIndex = 6;

        /// <summary>WebView2 でホストするスキンタブか（= WB 互換タブのみ）。</summary>
        public static bool IsWebSkinTab(int tabIndex) =>
            tabIndex == WbSkinTabIndex;

        public static bool IsWhiteBrowserCompatTab(int tabIndex) =>
            tabIndex == WbSkinTabIndex;

        /// <summary>WPF ネイティブスキンタブか。</summary>
        public static bool IsWpfSkinTab(int tabIndex) =>
            tabIndex == WpfSkinTabIndex;

        public static int GetThumbnailTabIndex(int tabIndex) =>
            tabIndex switch
            {
                WpfSkinTabIndex => WpfSkinThumbnailSlotIndex,
                WbSkinTabIndex => WhiteBrowserSkinSettings.GetThumbnailTabIndex(),
                _ => tabIndex,
            };

        public static SkinConfig GetDefaultConfig(int tabIndex) =>
            tabIndex switch
            {
                WbSkinTabIndex => SkinConfig.DefaultGridWeb(),
                _ => SkinConfig.DefaultGridWeb(),
            };

        public static string GetSkinFolderName(int tabIndex) =>
            tabIndex switch
            {
                WbSkinTabIndex => WhiteBrowserSkinSettings.ActiveSkinFolder,
                _ => WhiteBrowserSkinSettings.ActiveSkinFolder,
            };
    }
}
