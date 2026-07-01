using System.ComponentModel;

using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    public class DBInfo : INotifyPropertyChanged
    {
        private string currentDbFullPath = "";
        private string currentSkin = "";
        private string currentDbName = "";
        private string searchKeyword = "";
        private string sort = "";
        private string thumbFolder = "";
        private string bookmarkFolder = "";
        private string excludeExt = "";
        private int searchCount = 0;
        private SkinEngine currentSkinEngine = SkinEngine.Wpf;

        /// <summary>
        /// SQLiteデータベースのフルパス
        /// </summary>
        public string DBFullPath
        {
            get => currentDbFullPath;
            set { currentDbFullPath = value; OnPropertyChanged(nameof(DBFullPath)); }
        }

        /// <summary>
        /// 拡張子なしのデータベースファイル名（既存のサムネファイルを開く為）
        /// </summary>
        public string DBName
        {
            get => currentDbName;
            set { currentDbName = value; OnPropertyChanged(nameof(DBName)); }
        }

        /// <summary>
        /// スキン名（今やデフォルト4種のみ対応）
        /// </summary>
        public string Skin
        {
            get => currentSkin;
            set { currentSkin = value; OnPropertyChanged(nameof(Skin)); }
        }

        public string ThumbFolder
        {
            get => thumbFolder;
            set { thumbFolder = value; OnPropertyChanged(nameof(ThumbFolder)); }
        }

        public string BookmarkFolder
        {
            get => bookmarkFolder;
            set { bookmarkFolder = value; OnPropertyChanged(nameof(BookmarkFolder)); }
        }

        /// <summary>
        /// 走査から除外する拡張子（カンマ区切り。例: .jpg,.zip）。共通設定の対象拡張子のうちここに含まれるものを除外。
        /// </summary>
        public string ExcludeExt
        {
            get => excludeExt;
            set { excludeExt = value ?? ""; OnPropertyChanged(nameof(ExcludeExt)); }
        }

        public string SearchKeyword
        {
            get => searchKeyword;
            set { searchKeyword = value; OnPropertyChanged(nameof(SearchKeyword)); }
        }

        public SkinEngine CurrentSkinEngine
        {
            get => currentSkinEngine;
            set { currentSkinEngine = value; OnPropertyChanged(nameof(CurrentSkinEngine)); }
        }

        /// <summary>サムネ解決用の旧タブ番号（フェーズ B で廃止予定）。</summary>
        public int CurrentTabIndex => SkinEngineHelper.ToLegacyThumbTabIndex(currentSkinEngine);

        public string Sort
        {
            get => sort;
            set { sort = value; OnPropertyChanged(nameof(Sort)); }
        }

        public int SearchCount
        {
            get => searchCount;
            set { searchCount = value; OnPropertyChanged(nameof(SearchCount)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
