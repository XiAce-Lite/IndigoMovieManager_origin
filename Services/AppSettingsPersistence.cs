namespace IndigoMovieManager.Services
{
    /// <summary>
    /// Properties.Settings の更新と保存を集約する。
    /// </summary>
    internal static class AppSettingsPersistence
    {
        public static void SaveLastDoc(string path)
        {
            Properties.Settings.Default.LastDoc = path;
            Properties.Settings.Default.Save();
        }

        public static void SaveSkinEngineIfChanged(string engine)
        {
            if (!string.Equals(Properties.Settings.Default.LastSkinEngine, engine, StringComparison.OrdinalIgnoreCase))
            {
                Properties.Settings.Default.LastSkinEngine = engine;
                Properties.Settings.Default.Save();
            }
        }

        public static void SaveWbSkinSelection(string engine, string folder)
        {
            Properties.Settings.Default.LastSkinEngine = engine;
            Properties.Settings.Default.LastWbSkinFolder = folder;
            Properties.Settings.Default.Save();
        }

        public static void SaveWpfSkinSelection(string engine, string folder)
        {
            Properties.Settings.Default.LastSkinEngine = engine;
            Properties.Settings.Default.LastWpfSkinName = folder;
            Properties.Settings.Default.Save();
        }

        public static void SaveRecentFiles(IEnumerable<string> files)
        {
            Properties.Settings.Default.RecentFiles.Clear();
            Properties.Settings.Default.RecentFiles.AddRange([.. files]);
            Properties.Settings.Default.Save();
        }

        public static void SaveDmmTagExcludePatterns(string multilineText)
        {
            Properties.Settings.Default.DmmTagExcludePatterns = multilineText ?? "";
            Properties.Settings.Default.Save();
        }
    }
}
