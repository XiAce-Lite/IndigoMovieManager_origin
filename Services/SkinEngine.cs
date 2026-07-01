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
    }
}
