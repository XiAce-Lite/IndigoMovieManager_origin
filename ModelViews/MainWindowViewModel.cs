using System.Collections.ObjectModel;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace IndigoMovieManager.ModelViews
{
    public class MainWindowViewModel
    {
        public DBInfo DbInfo { get; set; }
        public ObservableCollection<NavigationDrawerItem> PrimaryNavItems { get; set; }
        public ObservableCollection<NavigationDrawerItem> RecentFileItems { get; set; }
        public ObservableCollection<NavigationDrawerItem> SettingsNavItems { get; set; }
        public ObservableCollection<NavigationDrawerItem> ToolNavItems { get; set; }
        public ObservableCollection<NavigationDrawerItem> ExitNavItems { get; set; }
        public ObservableCollection<MovieRecords> MovieRecs { get; set; }
        public ObservableCollection<MovieRecords> BookmarkRecs { get; set; }
        public ObservableCollection<History> HistoryRecs { get; set; }
        public ObservableCollection<SortItem> SortLists { get; set; }

        public MainWindowViewModel()
        {
            DbInfo = new DBInfo();
            PrimaryNavItems =
            [
                new NavigationDrawerItem
                {
                    Text = "新規作成",
                    Id = NavigationActionIds.New,
                    IconKind = PackIconKind.FolderAdd,
                },
                new NavigationDrawerItem
                {
                    Text = "ファイルを開く",
                    Id = NavigationActionIds.Open,
                    IconKind = PackIconKind.FolderOpen,
                },
            ];
            RecentFileItems = [];
            SettingsNavItems =
            [
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.CommonSettings,
                    Id = NavigationMenuIds.CommonSettings,
                    IconKind = PackIconKind.Settings,
                },
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.DatabaseSettings,
                    Id = NavigationMenuIds.DatabaseSettings,
                    IconKind = PackIconKind.Cogs,
                },
            ];
            ToolNavItems =
            [
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.WatchFolderEdit,
                    Id = NavigationMenuIds.WatchFolderEdit,
                    IconKind = PackIconKind.Binoculars,
                },
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.WatchFolderCheck,
                    Id = NavigationMenuIds.WatchFolderCheck,
                    IconKind = PackIconKind.Reload,
                },
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.RecreateAllThumbnails,
                    Id = NavigationMenuIds.RecreateAllThumbnails,
                    IconKind = PackIconKind.Image,
                },
                new NavigationDrawerItem
                {
                    Text = NavigationMenuIds.RefreshAllFileInfo,
                    Id = NavigationMenuIds.RefreshAllFileInfo,
                    IconKind = PackIconKind.FileDocumentOutline,
                },
            ];
            ExitNavItems =
            [
                new NavigationDrawerItem
                {
                    Text = "終了",
                    Id = NavigationActionIds.Exit,
                    IconKind = PackIconKind.ExitToApp,
                },
            ];
            MovieRecs = [];
            BookmarkRecs = [];
            HistoryRecs = [];
            BindingOperations.EnableCollectionSynchronization(MovieRecs, new object());

            SortLists =
            [
                new SortItem("0", "アクセス(新しい順)"),
                new SortItem("1", "アクセス(古い順)"),
                new SortItem("2", "ファイル(新しい順)"),
                new SortItem("3", "ファイル(古い順)"),
                new SortItem("6", "スコア(高い順)"),
                new SortItem("7", "スコア(低い順)"),
                new SortItem("8", "再生数(多い順)"),
                new SortItem("9", "再生数(少ない順)"),
                new SortItem("10", "名前かな(昇順)"),
                new SortItem("11", "名前かな(降順)"),
                new SortItem("12", "ファイル名(昇順)"),
                new SortItem("13", "ファイル名(降順)"),
                new SortItem("14", "ファイルパス(昇順)"),
                new SortItem("15", "ファイルパス(降順)"),
                new SortItem("16", "サイズ(大きい順)"),
                new SortItem("17", "サイズ(小さい順)"),
                new SortItem("18", "登録(新しい順)"),
                new SortItem("19", "登録(古い順)"),
                new SortItem("20", "再生時間(長い順)"),
                new SortItem("21", "再生時間(短い順)"),
                new SortItem("22", "コメント1(昇順)"),
                new SortItem("23", "コメント1(降順)"),
                new SortItem("24", "コメント2(昇順)"),
                new SortItem("25", "コメント2(降順)"),
                new SortItem("26", "コメント3(昇順)"),
                new SortItem("27", "コメント3(降順)"),
            ];
        }

        public class SortItem(string id, string name)
        {
            public string Id { get; set; } = id;
            public string Name { get; set; } = name;
        }
    }
}
