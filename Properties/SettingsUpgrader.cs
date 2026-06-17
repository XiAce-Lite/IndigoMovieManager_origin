using System.Configuration;
using System.IO;
using System.Reflection;

namespace IndigoMovieManager.Properties
{
    internal static class SettingsUpgrader
    {
        private static string MarkerPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndigoMovieManager",
                "settings-upgraded.version");

        public static void TryUpgrade(ApplicationSettingsBase settings)
        {
            string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
            string markerDirectory = Path.GetDirectoryName(MarkerPath)!;
            if (!Directory.Exists(markerDirectory))
            {
                Directory.CreateDirectory(markerDirectory);
            }

            if (File.Exists(MarkerPath))
            {
                string upgradedVersion = File.ReadAllText(MarkerPath).Trim();
                if (string.Equals(upgradedVersion, currentVersion, StringComparison.Ordinal))
                {
                    return;
                }
            }

            settings.Upgrade();
            settings.Save();
            File.WriteAllText(MarkerPath, currentVersion);
        }
    }
}
