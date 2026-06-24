using System.Data;
using IndigoMovieManager.Data;

namespace IndigoMovieManager
{
    /// <summary>
    /// DB の system テーブルに格納される個別設定（WhiteBrowser 互換）。
    /// Properties.Settings とは別物。
    /// </summary>
    public class DatabaseSettings
    {
        private string _thumbFolder = "";
        private string _bookmarkFolder = "";
        private int _keepHistory;
        private string _playerPrg = "";
        private string _playerParam = "";
        private string _excludeExt = "";
        private bool _thumbExists;
        private bool _bookmarkExists;
        private bool _keepHistoryExists;
        private bool _playerPrgExists;
        private bool _playerParamExists;
        private bool _excludeExtExists;

        public DatabaseSettings(string dbFullPath)
        {
            const string keys = "'thum','bookmark','keepHistory','playerPrg','playerParam','excludeExt'";
            DataTable systemTable = SqliteDataAccess.Query(dbFullPath, $"select * from system where attr in ({keys})");

            if (systemTable == null || systemTable.Rows.Count == 0)
            {
                return;
            }

            foreach (DataRow row in systemTable.Rows)
            {
                switch (row[0])
                {
                    case "thum":
                        _thumbFolder = row[1].ToString();
                        _thumbExists = true;
                        break;
                    case "bookmark":
                        _bookmarkFolder = row[1].ToString();
                        _bookmarkExists = true;
                        break;
                    case "keepHistory":
                        _keepHistory = Convert.ToInt32(row[1].ToString());
                        _keepHistoryExists = true;
                        break;
                    case "playerPrg":
                        _playerPrg = row[1].ToString();
                        _playerPrgExists = true;
                        break;
                    case "playerParam":
                        _playerParam = row[1].ToString();
                        _playerParamExists = true;
                        break;
                    case "excludeExt":
                        _excludeExt = row[1].ToString();
                        _excludeExtExists = true;
                        break;
                }
            }
        }

        public string ThumbFolder { get => _thumbFolder; set => _thumbFolder = value; }
        public string BookmarkFolder { get => _bookmarkFolder; set => _bookmarkFolder = value; }
        public string PlayerPrg { get => _playerPrg; set => _playerPrg = value; }
        public string PlayerParam { get => _playerParam; set => _playerParam = value; }
        public string ExcludeExt { get => _excludeExt; set => _excludeExt = value ?? ""; }
        public int KeepHistory { get => _keepHistory; set => _keepHistory = value; }

        public bool ThumbExists { get => _thumbExists; set => _thumbExists = value; }
        public bool PlayerPrgExists { get => _playerPrgExists; set => _playerPrgExists = value; }
        public bool BookmarkExists { get => _bookmarkExists; set => _bookmarkExists = value; }
        public bool KeepHistoryExists { get => _keepHistoryExists; set => _keepHistoryExists = value; }
        public bool PlayerParamExists { get => _playerParamExists; set => _playerParamExists = value; }
        public bool ExcludeExtExists { get => _excludeExtExists; set => _excludeExtExists = value; }
    }
}
