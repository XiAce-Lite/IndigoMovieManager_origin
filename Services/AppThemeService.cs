using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Themes;

namespace IndigoMovieManager.Services
{
    internal enum AppThemeMode
    {
        Light,
        Dark,
        System,
    }

    /// <summary>
    /// アプリ全体の Light/Dark/System テーマを適用する。
    /// </summary>
    internal static class AppThemeService
    {
        private static readonly PaletteHelper PaletteHelper = new();

        public static event EventHandler ThemeChanged;

        public static AppThemeMode Mode { get; private set; } = AppThemeMode.Light;

        public static bool IsDarkEffective { get; private set; }

        public static void InitializeFromSettings()
        {
            Mode = ParseMode(Properties.Settings.Default.ThemeMode);
            ApplyEffectiveTheme();
        }

        public static void SetMode(AppThemeMode mode)
        {
            Mode = mode;
            Properties.Settings.Default.ThemeMode = mode.ToString();
            ApplyEffectiveTheme();
        }

        public static void SetModeFromSettingsString(string value)
        {
            Mode = ParseMode(value);
            ApplyEffectiveTheme();
        }

        public static AppThemeMode ParseMode(string value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "dark" => AppThemeMode.Dark,
                "system" => AppThemeMode.System,
                _ => AppThemeMode.Light,
            };

        public static void HandleSystemThemeChanged()
        {
            if (Mode != AppThemeMode.System)
            {
                return;
            }

            ApplyEffectiveTheme();
        }

        public static void ApplyEffectiveTheme()
        {
            IsDarkEffective = ResolveEffectiveIsDark(Mode);
            ApplyMaterialDesignTheme(IsDarkEffective);
            ApplySharedResources(IsDarkEffective);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void ApplyDockTheme(DockingManager dockingManager)
        {
            if (dockingManager == null)
            {
                return;
            }

            dockingManager.Theme = IsDarkEffective
                ? new Vs2013DarkTheme()
                : new Vs2013LightTheme();
        }

        public static void ApplyHeaderZone(ColorZone headerZone)
        {
            if (headerZone == null)
            {
                return;
            }

            headerZone.Mode = IsDarkEffective ? ColorZoneMode.Dark : ColorZoneMode.PrimaryDark;
        }

        internal static bool ResolveEffectiveIsDark(AppThemeMode mode) =>
            mode switch
            {
                AppThemeMode.Dark => true,
                AppThemeMode.System => ReadSystemPrefersDark(),
                _ => false,
            };

        internal static bool ReadSystemPrefersDark()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                object value = key?.GetValue("AppsUseLightTheme");
                if (value is int dword)
                {
                    return dword == 0;
                }
            }
            catch
            {
                // 読めない環境ではライト扱い。
            }

            return false;
        }

        private static void ApplyMaterialDesignTheme(bool dark)
        {
            if (Application.Current == null)
            {
                return;
            }

            MaterialDesignThemes.Wpf.Theme theme = PaletteHelper.GetTheme();
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
            PaletteHelper.SetTheme(theme);
        }

        private static void ApplySharedResources(bool dark)
        {
            if (Application.Current?.Resources == null)
            {
                return;
            }

            ResourceDictionary resources = Application.Current.Resources;

            resources["ImmHeaderLabelForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0xE8, 0xE8, 0xE8));
            resources["ImmHeaderSecondaryForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xB0, 0xBE, 0xC5) : Color.FromRgb(0xCF, 0xD8, 0xDC));
            resources["ImmHeaderInputBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Colors.White);
            resources["ImmHeaderInputForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Colors.Black);
            resources["ImmHeaderDropdownItemBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Colors.White);
            resources["ImmHeaderDropdownItemForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Colors.Black);
            resources["ImmHeaderDropdownHighlightBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x45, 0x5A, 0x64) : Color.FromRgb(0xBB, 0xDE, 0xFB));

            resources["ImmHeaderSegmentGroupBorder"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x12, 0x18, 0x1C) : Color.FromRgb(0x1C, 0x26, 0x2B));
            resources["ImmHeaderSegmentGroupBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x26, 0x32, 0x38) : Color.FromRgb(0x37, 0x47, 0x4F));
            resources["ImmHeaderSegmentBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x45, 0x5A, 0x64) : Color.FromRgb(0x54, 0x6E, 0x7A));
            resources["ImmHeaderSegmentHoverBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x54, 0x6E, 0x7A) : Color.FromRgb(0x60, 0x7D, 0x8B));
            resources["ImmHeaderSegmentCheckedBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x0A, 0x0E, 0x10) : Color.FromRgb(0x12, 0x18, 0x1C));
            resources["ImmHeaderSegmentForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x90, 0xA4, 0xAE) : Color.FromRgb(0xCF, 0xD8, 0xDC));
            resources["ImmHeaderSegmentCheckedForeground"] = CreateFrozenBrush(Colors.White);
            resources["ImmHeaderSegmentRaisedHighlight"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x78, 0x90, 0x9C) : Color.FromRgb(0x90, 0xA4, 0xAE));
            resources["ImmHeaderSegmentRaisedShadow"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x26, 0x32, 0x38) : Color.FromRgb(0x37, 0x47, 0x4F));
            resources["ImmHeaderSegmentInsetShadow"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x00, 0x00, 0x00) : Color.FromRgb(0x0A, 0x0E, 0x10));
            resources["ImmHeaderSegmentInsetHighlight"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x54, 0x6E, 0x7A) : Color.FromRgb(0x78, 0x90, 0x9C));

            resources["ImmSettingsSegmentGroupBorder"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x12, 0x12, 0x12) : Color.FromRgb(0x9E, 0x9E, 0x9E));
            resources["ImmSettingsSegmentGroupBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Color.FromRgb(0xE0, 0xE0, 0xE0));
            resources["ImmSettingsSegmentBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x42, 0x42, 0x42) : Color.FromRgb(0xF5, 0xF5, 0xF5));
            resources["ImmSettingsSegmentHoverBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x4E, 0x4E, 0x4E) : Color.FromRgb(0xEE, 0xEE, 0xEE));
            resources["ImmSettingsSegmentCheckedBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x1A, 0x1A, 0x1A) : Color.FromRgb(0xB0, 0xB0, 0xB0));
            resources["ImmSettingsSegmentForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xB0, 0xBE, 0xC5) : Color.FromRgb(0x61, 0x61, 0x61));
            resources["ImmSettingsSegmentCheckedForeground"] = CreateFrozenBrush(
                dark ? Colors.White : Color.FromRgb(0x21, 0x21, 0x21));
            resources["ImmSettingsSegmentRaisedHighlight"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x61, 0x61, 0x61) : Colors.White);
            resources["ImmSettingsSegmentRaisedShadow"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x21, 0x21, 0x21) : Color.FromRgb(0xBD, 0xBD, 0xBD));
            resources["ImmSettingsSegmentInsetShadow"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x0A, 0x0A, 0x0A) : Color.FromRgb(0x75, 0x75, 0x75));
            resources["ImmSettingsSegmentInsetHighlight"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x61, 0x61, 0x61) : Color.FromRgb(0xE0, 0xE0, 0xE0));

            resources["ImmTagBarChipBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x37, 0x47, 0x4F) : Color.FromRgb(0xE3, 0xF2, 0xFD));
            resources["ImmTagBarChipBorder"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x54, 0x6E, 0x7A) : Color.FromRgb(0x90, 0xCA, 0xF9));
            resources["ImmTagBarChipForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xEC, 0xEF, 0xF1) : Color.FromRgb(0x21, 0x21, 0x21));
            resources["ImmTagBarChipSelectedBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x45, 0x5A, 0x64) : Color.FromRgb(0xBB, 0xDE, 0xFB));
            resources["ImmTagBarChipSelectedBorder"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x78, 0x90, 0x9C) : Color.FromRgb(0x42, 0xA5, 0xF5));

            resources["ImmTagChipBackground"] = CreateFrozenBrush(Color.FromRgb(0x90, 0xEE, 0x90));
            resources["ImmTagChipForeground"] = CreateFrozenBrush(
                dark ? Colors.Black : GetThemePrimaryMidColor());

            resources["ImmListHeaderBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Color.FromRgb(0xF0, 0xF0, 0xF0));
            resources["ImmListHeaderForeground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Colors.Black);
            resources["ImmListItemSelectedBackground"] = CreateFrozenBrush(
                dark ? Color.FromRgb(0x37, 0x47, 0x4F) : Color.FromRgb(0xE3, 0xF2, 0xFD));
            resources["ImmListItemSelectedBackgroundDarkSkin"] = CreateFrozenBrush(
                Color.FromRgb(0x37, 0x47, 0x4F));
        }

        private static Color GetThemePrimaryMidColor()
        {
            MaterialDesignThemes.Wpf.Theme theme = PaletteHelper.GetTheme();
            return theme.PrimaryMid.Color;
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
