using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using Microsoft.Win32;
using System.ComponentModel;
using System.Data;
using static IndigoMovieManager.SQLite;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace IndigoMovieManager
{
    /// <summary>
    /// WatchWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class WatchWindow : Window
    {
        private readonly WatchWindowViewModel WatchVM = new();
        private DataTable watchData;
        private readonly string _dbFullPath;

        public WatchWindow(string dbFullPath)
        {
            InitializeComponent();
            Closing += WatchWindowClosing;

            WatchFolderDmmAutoService.EnsureSchema(dbFullPath);
            GetWatchTable(dbFullPath);
            DataContext = WatchVM;

            _dbFullPath = dbFullPath;
        }

        private void WatchWindowClosing(object sender, CancelEventArgs e)
        {
            DeleteWatchTable(_dbFullPath);
            //データベースへ書き込む。
            foreach (WatchRecords item in WatchVM.WatchRecs)
            {
                if (string.IsNullOrEmpty(item.Dir)) { continue; }
                InsertWatchTable(_dbFullPath, item);
            }
        }

        private void GetWatchTable(string dbPath)
        {
            WatchVM.WatchRecs.Clear();
            if (!string.IsNullOrEmpty(dbPath))
            {
                watchData = GetData(dbPath, $"SELECT * FROM watch");
                var list = watchData.AsEnumerable().ToArray();
                foreach (var row in list)
                {
                    bool dmmAuto = watchData.Columns.Contains("dmm_auto")
                        && Convert.ToInt64(row["dmm_auto"]) == 1;
                    var item = new WatchRecords
                    {
                        Auto = (long)row["auto"] == 1,
                        Watch = (long)row["watch"] == 1,
                        Sub = (long)row["sub"] == 1,
                        DmmAuto = dmmAuto,
                        Dir = row["dir"].ToString()
                    };
                    WatchVM.WatchRecs.Add(item);
                }
            }
        }

        private void BtnReturn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            WatchRecords item = null;
            if (sender is FrameworkElement fe && fe.DataContext is WatchRecords fromButton)
            {
                item = fromButton;
            }
            else if (WatchDataGrid.SelectedItem is WatchRecords selected)
            {
                item = selected;
            }

            // 新規行プレースホルダでは DataContext が WatchRecords にならないため、ここで行を作る。
            if (item == null)
            {
                item = new WatchRecords();
                WatchVM.WatchRecs.Add(item);
                WatchDataGrid.SelectedItem = item;
            }

            string initial = string.IsNullOrWhiteSpace(item.Dir)
                ? Directory.GetCurrentDirectory()
                : item.Dir;

            var ofd = new OpenFolderDialog
            {
                InitialDirectory = initial,
                Multiselect = false,
                Title = "監視フォルダの選択",
            };

            if (ofd.ShowDialog() == true)
            {
                item.Dir = ofd.FolderName;
            }
        }
    }
}
