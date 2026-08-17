using System.ComponentModel;
using System.Runtime.CompilerServices;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 新規登録時に他スキン用サムネを先作りする対象スキンの設定（管理ファイル＝DB ごと）。
    /// system 属性: <c>preGenThumbs</c>（0/1）, <c>preGenThumbSkins</c>（Wpf:名|Wb:名…）。
    /// 実体が消えたスキンは一覧から除外する。
    /// </summary>
    internal static class PreGenThumbSkinSelection
    {
        public const string SystemAttrEnabled = "preGenThumbs";
        public const string SystemAttrSkinKeys = "preGenThumbSkins";
        public const string WpfPrefix = "Wpf:";
        public const string WbPrefix = "Wb:";
        public const char KeySeparator = '|';

        public sealed class SkinOption : INotifyPropertyChanged
        {
            private bool _isChecked;

            public string Key { get; init; }
            public string DisplayName { get; init; }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value)
                    {
                        return;
                    }

                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static bool ParseEnabled(string raw) =>
            string.Equals((raw ?? string.Empty).Trim(), "1", StringComparison.Ordinal)
            || string.Equals((raw ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase);

        public static string FormatEnabled(bool enabled) => enabled ? "1" : "0";

        public static string FormatWpfKey(string skinName) =>
            WpfPrefix + (skinName ?? string.Empty).Trim();

        public static string FormatWbKey(string folderName) =>
            WbPrefix + (folderName ?? string.Empty).Trim();

        public static bool TryParseKey(string key, out bool isWpf, out string name)
        {
            isWpf = false;
            name = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string trimmed = key.Trim();
            if (trimmed.StartsWith(WpfPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isWpf = true;
                name = trimmed[WpfPrefix.Length..].Trim();
                return name.Length > 0;
            }

            if (trimmed.StartsWith(WbPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isWpf = false;
                name = trimmed[WbPrefix.Length..].Trim();
                return name.Length > 0;
            }

            return false;
        }

        public static HashSet<string> ParseStoredKeys(string stored)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(stored))
            {
                return set;
            }

            foreach (string part in stored.Split(KeySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryParseKey(part, out _, out _))
                {
                    set.Add(part);
                }
            }

            return set;
        }

        public static string FormatStoredKeys(IEnumerable<string> selectedKeys)
        {
            if (selectedKeys == null)
            {
                return string.Empty;
            }

            return string.Join(
                KeySeparator,
                selectedKeys
                    .Where(k => TryParseKey(k, out _, out _))
                    .Select(k => k.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>ディスク上に存在するスキンのみ。削除済みは表示しない。</summary>
        public static IReadOnlyList<SkinOption> BuildOptionsFromDisk(ISet<string> checkedKeys = null)
        {
            var options = new List<SkinOption>();
            ISet<string> selected = checkedKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in WpfSkinLoader.EnumerateSkins())
            {
                string key = FormatWpfKey(name);
                options.Add(new SkinOption
                {
                    Key = key,
                    DisplayName = $"WPF: {name}",
                    IsChecked = ContainsKey(selected, key),
                });
            }

            foreach (string name in WhiteBrowserSkinSettings.EnumerateSkinFolders())
            {
                string key = FormatWbKey(name);
                options.Add(new SkinOption
                {
                    Key = key,
                    DisplayName = $"WB: {name}",
                    IsChecked = ContainsKey(selected, key),
                });
            }

            return options;
        }

        /// <summary>存在するスキンに限定した選択キー。</summary>
        public static HashSet<string> FilterExistingSelectedKeys(IEnumerable<string> selectedKeys)
        {
            HashSet<string> saved = selectedKeys == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(selectedKeys.Where(k => !string.IsNullOrWhiteSpace(k)), StringComparer.OrdinalIgnoreCase);
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SkinOption option in BuildOptionsFromDisk(saved))
            {
                if (option.IsChecked)
                {
                    existing.Add(option.Key);
                }
            }

            return existing;
        }

        /// <summary>
        /// 選択スキンからレイアウトを解決し、Key でユニーク化。解決できないスキンは飛ばす。
        /// </summary>
        public static IReadOnlyList<ThumbnailLayoutSpec> ResolveUniqueLayouts(IEnumerable<string> selectedKeys)
        {
            var byKey = new Dictionary<string, ThumbnailLayoutSpec>(StringComparer.OrdinalIgnoreCase);
            if (selectedKeys == null)
            {
                return [];
            }

            foreach (string raw in selectedKeys)
            {
                if (!TryResolveLayout(raw, out ThumbnailLayoutSpec layout) || layout == null)
                {
                    continue;
                }

                byKey.TryAdd(layout.Key, layout);
            }

            return [.. byKey.Values];
        }

        public static bool TryResolveLayout(string skinKey, out ThumbnailLayoutSpec layout)
        {
            layout = null;
            if (!TryParseKey(skinKey, out bool isWpf, out string name))
            {
                return false;
            }

            if (isWpf)
            {
                if (!WpfSkinLoader.TryLoad(name, out WpfSkinDefinition def) || def == null)
                {
                    return false;
                }

                layout = ThumbnailLayoutSpec.FromWpfSkinThumbnail(def.Thumbnail);
                return true;
            }

            IReadOnlyList<string> folders = WhiteBrowserSkinSettings.EnumerateSkinFolders();
            if (!folders.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            layout = ThumbnailLayoutSpec.FromSkinConfig(WhiteBrowserSkinSettings.ParseSkinConfig(name));
            return true;
        }

        private static bool ContainsKey(ISet<string> set, string key)
        {
            if (set == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return set.Contains(key);
        }
    }
}
