namespace IndigoMovieManager.Services
{
    public enum SkinEngine
    {
        Wpf,
        Wb,
    }

    internal static class SkinEngineHelper
    {
        public const string SettingWpf = "WPF";
        public const string SettingWb = "WB";

        public static SkinEngine FromSetting(string value) =>
            string.Equals(value, SettingWb, StringComparison.OrdinalIgnoreCase)
                ? SkinEngine.Wb
                : SkinEngine.Wpf;

        public static string ToSetting(SkinEngine engine) =>
            engine == SkinEngine.Wb ? SettingWb : SettingWpf;

        /// <summary>サムネ解決・キュー用の旧タブ番号（フェーズ B で廃止予定）。</summary>
        public static int ToLegacyThumbTabIndex(SkinEngine engine) =>
            engine == SkinEngine.Wb
                ? SkinTabIndexHelper.WbSkinTabIndex
                : SkinTabIndexHelper.WpfSkinTabIndex;

        public static SkinEngine FromLegacyThumbTabIndex(int tabIndex) =>
            tabIndex == SkinTabIndexHelper.WbSkinTabIndex ? SkinEngine.Wb : SkinEngine.Wpf;
    }
}
