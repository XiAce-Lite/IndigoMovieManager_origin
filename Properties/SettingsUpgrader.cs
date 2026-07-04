using System.Configuration;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Properties
{
    /// <summary>
    /// ユーザー設定を同一プロファイル内の前バージョンから引き継ぐ。
    /// マーカーは exe パスごとのプロファイル単位（別フォルダ起動で常用側が飛ばないようにする）。
    /// マーカー一致でも設定が初期値のままなら、前版の user.config から復旧を試みる。
    /// </summary>
    internal static class SettingsUpgrader
    {
        internal const string MarkerFileName = "settings-upgraded.version";

        private static readonly Point DefaultMainLocation = new(10, 10);
        private static readonly Size DefaultMainSize = new(800, 600);

        private static readonly Regex LastDocValueRegex = new(
            @"name\s*=\s*""LastDoc""[^>]*>\s*<value(?:\s*/>|[^>]*>(?<inner>[^<]*)</value>)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static void TryUpgrade(ApplicationSettingsBase settings)
        {
            string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            string userConfigPath = TryGetUserConfigPath();
            string profileDir = TryGetProfileDirectory(userConfigPath);

            if (string.IsNullOrEmpty(profileDir))
            {
                // プロファイル特定不能時は従来どおり一度 Upgrade する。
                settings.Upgrade();
                settings.Save();
                return;
            }

            Directory.CreateDirectory(profileDir);
            string markerPath = Path.Combine(profileDir, MarkerFileName);
            bool markerMatches = File.Exists(markerPath)
                && string.Equals(File.ReadAllText(markerPath).Trim(), currentVersion, StringComparison.Ordinal);

            if (!markerMatches)
            {
                settings.Upgrade();
                settings.Save();
            }

            // 保険: マーカー済みでも初期値のままなら、同じプロファイルの前版から復旧する。
            // （途中版が空のまま残っていると Upgrade() だけでは足りないことがある）
            if (LooksLikeFreshDefaults(settings))
            {
                string bestPrevious = FindBestPreviousUserConfig(profileDir, currentVersion);
                if (!string.IsNullOrEmpty(bestPrevious)
                    && !string.IsNullOrEmpty(userConfigPath))
                {
                    string currentDir = Path.GetDirectoryName(userConfigPath);
                    if (!string.IsNullOrEmpty(currentDir))
                    {
                        Directory.CreateDirectory(currentDir);
                        File.Copy(bestPrevious, userConfigPath, overwrite: true);
                        settings.Reload();
                        settings.Save();
                    }
                }
            }

            File.WriteAllText(markerPath, currentVersion);
        }

        internal static bool LooksLikeFreshDefaults(ApplicationSettingsBase settings)
        {
            if (settings is Settings typed)
            {
                return typed.MainLocation == DefaultMainLocation
                    && typed.MainSize == DefaultMainSize
                    && string.IsNullOrEmpty(typed.LastDoc);
            }

            try
            {
                object location = settings["MainLocation"];
                object size = settings["MainSize"];
                object lastDoc = settings["LastDoc"];
                return Equals(location, DefaultMainLocation)
                    && Equals(size, DefaultMainSize)
                    && string.IsNullOrEmpty(lastDoc as string);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 同じプロファイル内で、現在版以外の user.config のうち最も有望なものを返す。
        /// </summary>
        internal static string FindBestPreviousUserConfig(string profileDir, string currentVersion)
        {
            if (string.IsNullOrEmpty(profileDir) || !Directory.Exists(profileDir))
            {
                return null;
            }

            string bestPath = null;
            long bestLength = 0;
            Version bestVersion = null;

            foreach (string dir in Directory.GetDirectories(profileDir))
            {
                string name = Path.GetFileName(dir);
                if (string.Equals(name, currentVersion, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Version.TryParse(name, out Version version))
                {
                    continue;
                }

                string configPath = Path.Combine(dir, "user.config");
                if (!File.Exists(configPath))
                {
                    continue;
                }

                long length = new FileInfo(configPath).Length;
                // 初期化直後の薄い config（今回の事故では約 900 bytes）は候補にしない。
                if (length < 1500 || LooksLikeStoredDefaults(configPath))
                {
                    continue;
                }

                if (bestPath == null
                    || length > bestLength
                    || (length == bestLength && bestVersion != null && version > bestVersion))
                {
                    bestPath = configPath;
                    bestLength = length;
                    bestVersion = version;
                }
            }

            return bestPath;
        }

        internal static bool LooksLikeStoredDefaults(string userConfigPath)
        {
            try
            {
                string text = File.ReadAllText(userConfigPath);
                if (!HasDefaultWindow(text))
                {
                    return false;
                }

                Match match = LastDocValueRegex.Match(text);
                if (!match.Success)
                {
                    // LastDoc が無い、または空の自己閉じ value
                    return true;
                }

                string inner = match.Groups["inner"]?.Value;
                return string.IsNullOrWhiteSpace(inner);
            }
            catch
            {
                return true;
            }
        }

        internal static bool HasDefaultWindow(string userConfigText)
        {
            bool defaultLocation = userConfigText.Contains("<value>10, 10</value>", StringComparison.Ordinal)
                || userConfigText.Contains("<value>10,10</value>", StringComparison.Ordinal);
            bool defaultSize = userConfigText.Contains("<value>800, 600</value>", StringComparison.Ordinal)
                || userConfigText.Contains("<value>800,600</value>", StringComparison.Ordinal);
            return defaultLocation && defaultSize;
        }

        internal static string TryGetProfileDirectory(string userConfigPath)
        {
            if (string.IsNullOrEmpty(userConfigPath))
            {
                return null;
            }

            // ...\ProfileDir\1.0.0.78\user.config
            string versionDir = Path.GetDirectoryName(userConfigPath);
            if (string.IsNullOrEmpty(versionDir))
            {
                return null;
            }

            return Path.GetDirectoryName(versionDir);
        }

        private static string TryGetUserConfigPath()
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(
                    ConfigurationUserLevel.PerUserRoamingAndLocal);
                return config?.FilePath;
            }
            catch
            {
                return null;
            }
        }
    }
}
