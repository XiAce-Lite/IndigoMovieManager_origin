using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using Microsoft.Win32;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static IndigoMovieManager.SQLite;

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

        // データ5行 + 新規入力行1行
        private const int VisibleDataRows = 5;
        private const int VisibleNewItemRows = 1;

        public WatchWindow(string dbFullPath)
        {
            InitializeComponent();
            Closing += WatchWindowClosing;
            Loaded += WatchWindow_Loaded;

            WatchFolderDmmAutoService.EnsureSchema(dbFullPath);
            GetWatchTable(dbFullPath);
            DataContext = WatchVM;

            _dbFullPath = dbFullPath;
        }

        private void WatchWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 24);

            WatchDataGrid.UpdateLayout();

            double headerH = WatchDataGrid.ColumnHeaderHeight > 0
                ? WatchDataGrid.ColumnHeaderHeight
                : 52;
            double rowH = WatchDataGrid.RowHeight > 0
                ? WatchDataGrid.RowHeight
                : 32;

            // データ5行 + 新規行1行がちょうど見える高さ
            int visibleRows = VisibleDataRows + VisibleNewItemRows;
            WatchDataGrid.Height = headerH + (rowH * visibleRows) + 4;

            // SizeToCells の実測後にフォルダ列の初期幅を決める（ユーザーはヘッダー境界で調整可）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WatchDataGrid.UpdateLayout();
                double iconCols = 0;
                int colCount = WatchDataGrid.Columns.Count;
                if (colCount >= 2)
                {
                    iconCols = WatchDataGrid.Columns[colCount - 2].ActualWidth
                        + WatchDataGrid.Columns[colCount - 1].ActualWidth;
                }
                if (iconCols < 1)
                {
                    iconCols = 64;
                }

                double scrollPad = SystemParameters.VerticalScrollBarWidth + 4;
                double gridViewport = Math.Max(200, ActualWidth - 40 - scrollPad);
                double fixedCols = 68 + 56 + 88 + 82 + iconCols;
                watchFolder.Width = Math.Max(80, gridViewport - fixedCols);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void WatchWindowClosing(object sender, CancelEventArgs e)
        {
            DeleteWatchTable(_dbFullPath);
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

        private void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not WatchRecords item)
            {
                return;
            }

            TryDeleteWatchItem(item);
        }

        private void WatchDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete)
            {
                return;
            }

            if (WatchDataGrid.SelectedItem is not WatchRecords item)
            {
                return;
            }

            if (TryDeleteWatchItem(item))
            {
                e.Handled = true;
            }
        }

        private bool TryDeleteWatchItem(WatchRecords item)
        {
            if (item == null || !WatchVM.WatchRecs.Contains(item))
            {
                return false;
            }

            string pathLabel = string.IsNullOrWhiteSpace(item.Dir) ? "（未設定）" : item.Dir.Trim();
            var confirm = new MessageBoxEx(this)
            {
                DlogTitle = "監視フォルダの削除",
                DlogMessage = $"次の監視フォルダを一覧から削除します。よろしいですか？\n\n{pathLabel}",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.DeleteOutline,
                OkOnly = false,
                PreferCancelFocus = true,
            };
            confirm.ShowDialog();
            if (confirm.CloseStatus() != MessageBoxResult.OK)
            {
                return false;
            }

            WatchVM.WatchRecs.Remove(item);
            return true;
        }
    }
}
