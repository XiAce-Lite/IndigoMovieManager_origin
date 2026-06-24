using MaterialDesignThemes.Wpf;

namespace IndigoMovieManager
{
    /// <summary>
    /// ドロワー内ナビゲーション行（アイコン + ラベル）。
    /// </summary>
    public class NavigationDrawerItem
    {
        public string Text { get; init; } = "";

        /// <summary>NavigationMenuIds、ファイルパス、action:* など。</summary>
        public string Id { get; init; } = "";

        public PackIconKind IconKind { get; init; } = PackIconKind.Circle;

        public static NavigationDrawerItem ForRecentFile(string path) =>
            new()
            {
                Text = path,
                Id = path,
                IconKind = PackIconKind.File,
            };
    }
}
