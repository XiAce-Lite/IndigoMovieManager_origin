using System.IO;
using System.Text.RegularExpressions;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// WhiteBrowser 互換スキンのホスト設定。
    /// <see cref="ActiveSkinFolder"/> で読み込むスキンを切り替える（テスト用に実行時変更可）。
    /// </summary>
    internal static class WhiteBrowserSkinSettings
    {
        public const string WbHostVirtualHost = "imm-wb.local";
        public const string DefaultSkinFolder = "DefaultGrid";

        private static readonly Regex ConfigValueRegex = new(
            @"thum-(?<key>width|height|column|row)\s*:\s*(?<value>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Skins/WbHost 直下のスキンフォルダ名（DefaultGrid / DefaultSmall 等）。</summary>
        public static string ActiveSkinFolder { get; set; } = DefaultSkinFolder;

        public static string GetWbHostRoot() =>
            Path.Combine(AppContext.BaseDirectory, "Skins", "WbHost");

        public static string GetCompatScriptPath() =>
            Path.Combine(GetWbHostRoot(), "imm-wb-compat.js");

        public static string GetEntryUrl()
        {
            string folder = ActiveSkinFolder;
            return $"https://{WbHostVirtualHost}/{folder}/{folder}.htm";
        }

        public static IReadOnlyList<string> EnumerateSkinFolders()
        {
            string root = GetWbHostRoot();
            if (!Directory.Exists(root))
            {
                return [];
            }

            return [.. Directory.GetDirectories(root)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name)
                    && File.Exists(Path.Combine(root, name!, $"{name}.htm")))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)!];
        }

        public static SkinConfig ParseSkinConfig(string folder)
        {
            string htmPath = Path.Combine(GetWbHostRoot(), folder, $"{folder}.htm");
            var config = SkinConfig.DefaultGridWeb();
            if (!File.Exists(htmPath))
            {
                return config;
            }

            string text = File.ReadAllText(htmPath);
            int width = 0;
            int height = 0;
            int column = 0;
            int row = 0;

            foreach (Match match in ConfigValueRegex.Matches(text))
            {
                if (!int.TryParse(match.Groups["value"].Value, out int value))
                {
                    continue;
                }

                switch (match.Groups["key"].Value.ToLowerInvariant())
                {
                    case "width": width = value; break;
                    case "height": height = value; break;
                    case "column": column = value; break;
                    case "row": row = value; break;
                }
            }

            return new SkinConfig
            {
                ThumbWidth = width > 0 ? width : config.ThumbWidth,
                ThumbHeight = height > 0 ? height : config.ThumbHeight,
                ThumbColumn = column > 0 ? column : config.ThumbColumn,
                ThumbRow = row > 0 ? row : config.ThumbRow,
                MultiSelect = 1,
            };
        }

        /// <summary>スキン #config のサムネサイズに対応する物理サムネタブ（0=Small, 2=Grid 等）。</summary>
        public static int GetThumbnailTabIndex() =>
            MapSkinConfigToThumbnailTab(ParseSkinConfig(ActiveSkinFolder));

        public static string GetThumbnailTag() =>
            FormatThumbnailTag(ParseSkinConfig(ActiveSkinFolder));

        public static int MapSkinConfigToThumbnailTab(SkinConfig config)
        {
            // スキンの #config (W×H×列×行) を既存の物理サムネタブと突き合わせる。
            for (int tab = 0; tab < SkinTabIndexHelper.PhysicalThumbTabCount; tab++)
            {
                var info = new TabInfo(tab, "");
                if (info.Width == config.ThumbWidth
                    && info.Height == config.ThumbHeight
                    && info.Columns == config.ThumbColumn
                    && info.Rows == config.ThumbRow)
                {
                    return tab;
                }
            }

            // 完全一致が無い場合は列数・行数が一致する近いレイアウトを優先する。
            int bestTab = -1;
            long bestDiff = long.MaxValue;
            for (int tab = 0; tab < SkinTabIndexHelper.PhysicalThumbTabCount; tab++)
            {
                var info = new TabInfo(tab, "");
                if (info.Columns != config.ThumbColumn || info.Rows != config.ThumbRow)
                {
                    continue;
                }

                long diff = Math.Abs((long)info.Width * info.Height - (long)config.ThumbWidth * config.ThumbHeight);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestTab = tab;
                }
            }

            if (bestTab >= 0)
            {
                return bestTab;
            }

            return config.ThumbWidth * config.ThumbHeight <= 120 * 90 ? 0 : 2;
        }

        private static string FormatThumbnailTag(SkinConfig config) =>
            $"{config.ThumbWidth}x{config.ThumbHeight}x{config.ThumbColumn}x{config.ThumbRow}";
    }
}
