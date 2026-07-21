namespace IndigoMovieManager
{
    /// <summary>
    /// メニュー・ツリーの Tag / Text 用文字列定数。
    /// </summary>
    internal static class NavigationMenuIds
    {
        public const string SettingsRoot = "設定";
        public const string CommonSettings = "共通設定";
        public const string DatabaseSettings = "個別設定";

        public const string ToolsRoot = "ツール";
        public const string WatchFolderEdit = "監視フォルダ編集";
        public const string WatchFolderCheck = "監視フォルダ更新チェック";
        public const string RecreateAllThumbnails = "全ファイルサムネイル再作成";
        public const string RefreshAllFileInfo = "全ファイル情報再取得";
        public const string DmmBulkFetch = "DMM 情報を一括取得";
        public const string DmmPendingCandidates = "DMM 未確定候補を処理";
    }
}
