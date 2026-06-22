using AvalonDock;
using AvalonDock.Layout.Serialization;
using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using IndigoMovieManager.Data;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Notification.Wpf;
using OpenCvSharp;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.SQLite;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IMainWindowActions, IMainWindowTabViews
    {
        //監視モードは FolderCheckMode（Services/FolderCheckService.cs）を使用
        private Task _thumbCheckTask;
        private CancellationTokenSource _thumbCheckCts = new();
        private readonly ThumbnailQueueProcessor _thumbnailQueueProcessor = new();
        private readonly ThumbnailQueueScheduler _thumbnailScheduler = new();
        private readonly FileWatcherManager _fileWatcherManager = new();

        private const string RECENT_OPEN_FILE_LABEL = "最近開いたファイル";
        private Stack<string> recentFiles = new();

        private IEnumerable<MovieRecords> filterList = [];

        private DataTable systemData;
        private bool _movieRecordsLoaded;
        private readonly MovieListCoordinator _movieListCoordinator = new();
        private DataTable historyData;
        private DataTable watchData;
        private DataTable bookmarkData;

        // MainWindow クラス内の MainVM フィールドまたはプロパティの宣言を public に変更
        public readonly MainWindowViewModel MainVM;
        internal System.Windows.Point lbClickPoint = new();

        private DateTime _lastSliderTime = DateTime.MinValue;
        private readonly TimeSpan _timeSliderInterval = TimeSpan.FromSeconds(0.1);

        //private DateTime _lastInputTime = DateTime.MinValue;  //インクリメントサーチで使用。一旦オミット。
        private readonly TimeSpan _timeInputInterval = TimeSpan.FromSeconds(0.5);

        //結局、タイマー方式で動画とマニュアルサムネイルのスライダーを同期させた
        private readonly DispatcherTimer timer;
        private readonly ManualThumbnailPreviewController _manualPreview;
        private bool isDragging = false;

        //マニュアルサムネイル時の右クリックしたカラムの返却を受け取る変数
        private int manualPos = 0;

        //IME起動中的なフラグ。日本語入力中（未変換）にインクリメンタルサーチさせない為。
        private bool _imeFlag = false;

        private readonly ThumbnailLayoutCache _thumbLayoutCache = new();

        //private bool _searchBoxItemSelectedByMouse = false;
        private bool _searchBoxItemSelectedByUser = false;
        private bool _isDeletingSearchHistory = false;
        private bool _isApplyingSearchKeyword = false;
        private int _fileInfoRefreshRunning = 0;

        public MainWindow()
        {
            MainVM = new MainWindowViewModel(); // ← 追加
            
            Properties.SettingsUpgrader.TryUpgrade(Properties.Settings.Default);

            recentFiles.Clear();

            InitializeComponent();

            // アセンブリのファイルバージョンを取得
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

            this.Title = $"Indigo Movie Manager v{version}";

            ContentRendered += MainWindow_ContentRendered;
            Closing += MainWindow_Closing;
            TextCompositionManager.AddPreviewTextInputHandler(SearchBox, OnPreviewTextInput);
            TextCompositionManager.AddPreviewTextInputStartHandler(SearchBox, OnPreviewTextInputStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(SearchBox, OnPreviewTextInputUpdate);

            var rootItem = new TreeSource() { Text = RECENT_OPEN_FILE_LABEL, IsExpanded = false };
            MainVM.RecentTreeRoot.Add(rootItem);

            if (Properties.Settings.Default.RecentFiles != null)
            {
                recentFiles = RecentFilesService.LoadFromSettings(Properties.Settings.Default.RecentFiles);
                RecentFilesService.PopulateTreeChildren(recentFiles, rootItem);
            }

            DataContext = MainVM;

            if (Path.Exists("layout.xml"))
            {
                XmlLayoutSerializer layoutSerializer = new(uxDockingManager);
                using var reader = new StreamReader("layout.xml");
                layoutSerializer.Deserialize(reader);
            }

            #region Player Initialize
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            timer.Tick += new EventHandler(Timer_Tick);

            _manualPreview = new ManualThumbnailPreviewController(Dispatcher);
            _manualPreview.OnFrameReady = source => uxPreviewImage.Source = source;

            uxTime.Text = "00:00:00";
            uxVolume.Text = ((int)(uxVolumeSlider.Value * 100)).ToString();
            PlayerArea.Visibility = Visibility.Collapsed;
            PlayerController.Visibility = Visibility.Collapsed;
            uxPreviewImage.Visibility = Visibility.Collapsed;
            #endregion
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                _ = Task.Run(ClearTempJpg);

                //ロケーションとサイズの復元
                Left = Properties.Settings.Default.MainLocation.X;
                Top = Properties.Settings.Default.MainLocation.Y;
                Width = Properties.Settings.Default.MainSize.Width;
                Height = Properties.Settings.Default.MainSize.Height;

                //前回起動時のファイルを開く処理
                if (Properties.Settings.Default.AutoOpen)
                {
                    if (Properties.Settings.Default.LastDoc != null)
                    {
                        if (Path.Exists(Properties.Settings.Default.LastDoc))
                        {
                            if (Properties.Settings.Default.AutoOpen)
                            {
                                _ = OpenDatafileAsync(Properties.Settings.Default.LastDoc);
                            }
                        }
                    }
                }

                // サムネイル監視タスクを一度だけ起動
                if (_thumbCheckTask == null || _thumbCheckTask.IsCompleted)
                {
                    _thumbCheckTask = CheckThumbAsync(_thumbCheckCts.Token);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (Properties.Settings.Default.ConfirmExit)
            {
                var result = MessageBox.Show(this, "本当に終了しますか？", "終了確認", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result != MessageBoxResult.OK)
                {
                    e.Cancel = true;
                    MenuToggleButton.IsChecked = false;
                    return;
                }
            }

            try
            {
                Properties.Settings.Default.MainLocation = new System.Drawing.Point((int)Left, (int)Top);
                Properties.Settings.Default.MainSize = new System.Drawing.Size((int)Width, (int)Height);
                UpdateSkin();
                UpdateSort();

                Properties.Settings.Default.RecentFiles.Clear();
                Properties.Settings.Default.RecentFiles.AddRange([.. recentFiles.Reverse()]);
                Properties.Settings.Default.Save();

                XmlLayoutSerializer layoutSerializer = new(uxDockingManager);
                using var writer = new StreamWriter("layout.xml");
                layoutSerializer.Serialize(writer);

                if (!string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                {
                    var keepHistoryData = SelectSystemTable("keepHistory");
                    int keepHistoryCount = Convert.ToInt32(keepHistoryData == "" ? "30" : keepHistoryData);
                    DeleteHistoryTable(MainVM.DbInfo.DBFullPath, keepHistoryCount);
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _thumbCheckCts.Cancel();
            }
        }

        private void ClearThumbnailQueue() => _thumbnailScheduler.ClearQueue();

        private void EnqueueThumbnailWork(IReadOnlyList<QueueObj> items, int primaryTabIndex, bool beginNewJob = false) =>
            _thumbnailScheduler.EnqueueWork(items, primaryTabIndex, beginNewJob);

        private void EnqueueThumbnailWork(QueueObj item, int primaryTabIndex, bool beginNewJob = false) =>
            _thumbnailScheduler.EnqueueWork(item, primaryTabIndex, beginNewJob);

        private void EnqueueSilentThumbnailWork(QueueObj item) =>
            _thumbnailScheduler.EnqueueSilentWork(item);

        private void StartTabSwitchThumbnailJob(int tabIndex) =>
            _thumbnailScheduler.StartTabSwitchJob(tabIndex, filterList, _thumbLayoutCache);

        private static int GetThumbnailQueueMaxParallelism() => ThumbnailQueueScheduler.GetMaxParallelism();

        private void RestartThumbnailTask()
        {
            ClearThumbnailQueue();
            _thumbCheckCts.Cancel();
            _thumbCheckCts = new CancellationTokenSource();
            _thumbCheckTask = CheckThumbAsync(_thumbCheckCts.Token);
        }

        /// <summary>
        /// ファイル追加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var ext = Path.GetExtension(e.FullPath);
                string checkExt = Properties.Settings.Default.CheckExt.Replace("*", "");
                string[] checkExts = checkExt.Split(",");

                if (checkExts.Contains(ext))
                {
                    if (e.ChangeType == WatcherChangeTypes.Created)
                    {
                        // ファイルが使用中の場合のリトライ処理
                        const int maxRetry = 10;
                        int retry = 0;
                        bool fileReady = false;
                        while (retry < maxRetry)
                        {
                            try
                            {
                                using var stream = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                fileReady = true;
                                break;
                            }
                            catch (IOException)
                            {
                                Thread.Sleep(1000);
                                retry++;
                            }
                        }
                        if (!fileReady)
                        {
#if DEBUG
                            Debug.WriteLine($"ファイル {e.FullPath} にアクセスできません。");
#endif
                            return;
                        }

                        MovieInfo mvi = new(e.FullPath);
                        _ = InsertMovieTable(MainVM.DbInfo.DBFullPath, mvi);
                        DataTable dt = GetData(MainVM.DbInfo.DBFullPath, "select * from movie order by movie_id desc");
                        if (dt.Rows.Count > 0)
                        {
                            DataRowToViewData(dt.Rows[0]);
                        }

                        QueueObj newFileForThumb = new()
                        {
                            MovieId = mvi.MovieId,
                            MovieFullPath = mvi.MoviePath,
                            Tabindex = MainVM.DbInfo.CurrentTabIndex
                        };
                        EnqueueThumbnailWork(newFileForThumb, MainVM.DbInfo.CurrentTabIndex, beginNewJob: true);
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"FileChangedで例外発生: {ex.Message}");
#endif
                MessageBox.Show(this, $"ファイル変更の処理中にエラーが発生しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(); // アプリケーションを終了
            }
        }

        /// <summary>
        /// ファイル名変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileRenamed(object sender, RenamedEventArgs e)
        {
            var ext = Path.GetExtension(e.FullPath);
            string checkExt = Properties.Settings.Default.CheckExt.Replace("*", "");
            string[] checkExts = checkExt.Split(",");
            var eFullPath = e.FullPath;
            var oldFullPath = e.OldFullPath;

            if (checkExts.Contains(ext))
            {
#if DEBUG
                string s = string.Format($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} :");
                s += $"【{e.ChangeType}】{e.OldName} → {e.FullPath}";
                Debug.WriteLine(s);
#endif
                //本家では、Renameは即反映してる様子。
                //このタイミングでは、新旧のファイル名がフルパスで取得可能。
                //旧ファイル名でDB検索、対象がヒットしたら、新ファイル名に変更。
                RenameThumb(eFullPath, oldFullPath);
            }
        }

        private void RunWatcher(string watchFolder, bool sub) =>
            _fileWatcherManager.AddWatcher(watchFolder, sub, FileChanged, FileRenamed);

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            _imeFlag = false;
        }
        private void OnPreviewTextInputStart(object sender, TextCompositionEventArgs e)
        {
            _imeFlag = true;
        }
        private void OnPreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
        {
            if (e.TextComposition.CompositionText.Length == 0) { _imeFlag = false; }
        }

        //todo : And以外の検索の実装。せめてNOT検索ぐらいまでは…
        //todo : 検索履歴の保管条件（おそらくヒット：ゼロ件超で保管）確認＆修正
        //todo : タグバー代替（保管済み検索条件）の実装
        //stack : プロパティ表示ウィンドウの作成。
        //todo : 重複チェック。本家は恐らくファイル名もチェックで使ってる模様。
        //       こっちで登録しても再度本家に登録されるケースがあったのは、ファイル名の大文字小文字が違ってたから。
        //       movie_name と Hash で重複チェックかなぁ。
        //       本家のmovie_nameは小文字変換かけてる模様。合わせてみたら再登録されなかったので恐らく正解。

        private void OpenDatafile(string dbFullPath)
        {
            _ = OpenDatafileAsync(dbFullPath);
        }

        private async Task OpenDatafileAsync(string dbFullPath)
        {
            //強制的に-1にする。前回のタブが0だった場合の対応
            Tabs.SelectedIndex = -1;
            ClearThumbnailQueue();
            watchData?.Clear();
            _fileWatcherManager.Clear();
            MainVM.DbInfo.SearchKeyword = "";
            _movieRecordsLoaded = false;

            MainVM.DbInfo.DBName = Path.GetFileNameWithoutExtension(dbFullPath);
            MainVM.DbInfo.DBFullPath = dbFullPath;

            using (var session = new SQLiteSession(dbFullPath))
            {
                GetSystemTable(dbFullPath, session);
                RefreshThumbPathCache();
                MainVM.MovieRecs.Clear();
                GetHistoryTable(dbFullPath, session);

                int startupTabIndex = ThumbnailLayoutCache.GetTabIndexFromSkin(MainVM.DbInfo.Skin);
                string sortId = MainVM.DbInfo.Sort ?? "1";
                await FilterAndSortAsync(sortId, true, startupTabIndex).ConfigureAwait(true);

                if (MainVM.DbInfo.Skin != null)
                {
                    SwitchTab(MainVM.DbInfo.Skin);
                }

                GetBookmarkTable(session);
            }

            CreateWatcher();
            ScheduleStartupFolderCheck();
        }

        private void SetLoadingOverlayVisible(bool visible)
        {
            LoadingOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private int GetDefaultResolveTabIndex()
        {
            return MainVM.DbInfo.CurrentTabIndex >= 0
                ? MainVM.DbInfo.CurrentTabIndex
                : ThumbnailLayoutCache.GetTabIndexFromSkin(MainVM.DbInfo.Skin);
        }

        private async Task ReloadMovieRecordsAsync(string sortId, int? resolveTabIndexOnly = null)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            SetLoadingOverlayVisible(true);
            try
            {
                int tabCount = Tabs?.Items?.Count ?? 5;
                int tabIndex = resolveTabIndexOnly ?? GetDefaultResolveTabIndex();
                MovieListCoordinator.ReloadResult loaded = await _movieListCoordinator.ReloadAsync(
                    MainVM.DbInfo.DBFullPath,
                    sortId,
                    _thumbLayoutCache,
                    tabCount,
                    tabIndex).ConfigureAwait(true);

                MovieListCoordinator.ReplaceCollection(MainVM.MovieRecs, loaded.Records);
                _movieRecordsLoaded = true;
            }
            finally
            {
                SetLoadingOverlayVisible(false);
            }
        }

        private void RefreshThumbPathCache()
        {
            int tabCount = Tabs?.Items?.Count ?? 5;
            _thumbLayoutCache.Refresh(MainVM.DbInfo.DBName, MainVM.DbInfo.ThumbFolder, tabCount);
        }

        private void ScheduleStartupFolderCheck()
        {
            _ = Dispatcher.InvokeAsync(async () =>
            {
                await CheckFolderAsync(FolderCheckMode.Auto);
            }, DispatcherPriority.ApplicationIdle);
        }

        public string SelectSystemTable(string attr)
        {
            return SystemTableService.SelectValue(systemData, attr);
        }

        private void GetBookmarkTable(SQLiteSession session = null)
        {
            bookmarkData = QueryDb(MainVM.DbInfo.DBFullPath, "SELECT * FROM bookmark", session);
            BookmarkService.LoadInto(
                bookmarkData,
                MainVM.BookmarkRecs,
                MainVM.DbInfo.BookmarkFolder,
                MainVM.DbInfo.DBName);
        }

        private void GetHistoryTable(string dbFullPath, SQLiteSession session = null)
        {
            // 現在のテキストを一時保存
            var currentText = SearchBox.Text;

            // find_textごとに最新の1件のみ取得
            historyData = QueryDb(dbFullPath, HistoryService.LatestPerKeywordSql, session);
            if (historyData != null)
            {
                HistoryService.LoadInto(historyData, MainVM.HistoryRecs);
            }
            // テキストを復元
            SearchBox.Text = currentText;
        }

        private void PromoteSearchHistory(string keyword) =>
            HistoryService.PromoteSearchHistory(MainVM.HistoryRecs, keyword);

        private void GetSystemTable(string dbPath, SQLiteSession session = null)
        {
            if (!string.IsNullOrEmpty(dbPath))
            {
                systemData = QueryDb(dbPath, "SELECT attr, value FROM system", session);
                SystemTableService.ApplyToDbInfo(systemData, MainVM.DbInfo);
            }
            else
            {
                systemData?.Clear();
            }
        }

        private static DataTable QueryDb(string dbPath, string sql, SQLiteSession session = null) =>
            DatabaseQueryHelper.Query(dbPath, sql, session);

        private void GetWatchTable(string dbPath, string sql, SQLiteSession session = null)
        {
            if (!string.IsNullOrEmpty(dbPath))
            {
                watchData = QueryDb(dbPath, sql, session);
            }
        }

        private void UpdateSort()
        {
            if (!string.IsNullOrEmpty(MainVM.DbInfo.Sort))
            {
                UpsertSystemTable(Properties.Settings.Default.LastDoc, "sort", MainVM.DbInfo.Sort);
            }
        }

        private void UpdateSkin()
        {
            //5x2はあえて書き込まない。互換性の関係で。
            string tabName = Tabs.SelectedIndex switch
            {
                0 => "DefaultSmall",
                1 => "DefaultBig",
                2 => "DefaultGrid",
                3 => "DefaultList",
                _ => "DefaultSmall",
            };
            UpsertSystemTable(Properties.Settings.Default.LastDoc, "skin", tabName);
        }

        private void SwitchTab(string skin) => TabSelectionHelper.SwitchTab(this, skin);

        public void SelectFirstItem() => TabSelectionHelper.SelectFirstItem(this);

        private void Refresh() => TabSelectionHelper.RefreshLists(this);

        private async void RenameThumb(string eFullPath, string oldFullPath)
        {
            try
            {
                foreach (var item in MainVM.MovieRecs.Where(x => x.Movie_Path == oldFullPath))
                {
                    item.Movie_Path = eFullPath;
                    item.Movie_Name = Path.GetFileNameWithoutExtension(eFullPath).ToLower();

                    //DB内のデータ更新＆サムネイルのファイル名変更処理
                    UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, item.Movie_Id, "movie_path", item.Movie_Path);
                    UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, item.Movie_Id, "movie_name", item.Movie_Name);

                    //サムネイルのリネーム
                    var checkFileName = Path.GetFileNameWithoutExtension(oldFullPath);
                    MovieRenameService.RenameThumbnailFiles(
                        MainVM.DbInfo.ThumbFolder,
                        MainVM.DbInfo.DBName,
                        checkFileName,
                        item.Movie_Name,
                        item.Hash,
                        item);

                    if (Path.Exists(BookmarkRecordMapper.ResolveBookmarkFolder(MainVM.DbInfo.BookmarkFolder, MainVM.DbInfo.DBName)))
                    {
                        MovieRenameService.RenameBookmarkFiles(
                            MainVM.DbInfo.BookmarkFolder,
                            MainVM.DbInfo.DBName,
                            checkFileName,
                            item.Movie_Name);
                        UpdateBookmarkRename(MainVM.DbInfo.DBFullPath, checkFileName, item.Movie_Name);
                    }
                }
                GetBookmarkTable();
                BookmarkList.Items.Refresh();
                await FilterAndSortAsync(MainVM.DbInfo.Sort, true).ConfigureAwait(true);
                Refresh();
            }
            catch (Exception)
            {
            }
        }

        private async Task FilterAndSortAsync(string id, bool isGetNew = false, int? resolveTabIndexOnly = null)
        {
            if (!_movieRecordsLoaded || isGetNew)
            {
                await ReloadMovieRecordsAsync(id, resolveTabIndexOnly).ConfigureAwait(true);
            }

            await ApplyFilterAndSortAsync(id).ConfigureAwait(true);
        }

        private async Task ApplyFilterAndSortAsync(string id)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            List<MovieRecords> snapshot = [.. MainVM.MovieRecs];
            string searchKeyword = MainVM.DbInfo.SearchKeyword ?? "";

            MovieListCoordinator.FilterApplyResult result = await Task.Run(() =>
                MovieListCoordinator.ApplyFilter(snapshot, searchKeyword, id)).ConfigureAwait(true);

            filterList = result.Items;
            MainVM.DbInfo.SearchCount = result.SearchCount;

            viewExtDetail.Visibility = MainVM.DbInfo.SearchCount == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            SmallList.ItemsSource = filterList;
            BigList.ItemsSource = filterList;
            GridList.ItemsSource = filterList;
            ListDataGrid.ItemsSource = filterList;
            BigList10.ItemsSource = filterList;
            Refresh();
#if DEBUG
            sw.Stop();
            Debug.WriteLine($"絞り込み経過時間 FilterAndSort：{sw.ElapsedMilliseconds} ミリ秒");
#endif
        }

        private void DataRowToViewData(DataRow row, int? resolveTabIndexOnly = null)
        {
            int tabCount = Tabs?.Items?.Count ?? _thumbLayoutCache.TabOutPaths.Length;
            MainVM.MovieRecs.Add(
                MovieRecordMapper.FromDataRow(row, _thumbLayoutCache, tabCount, resolveTabIndexOnly)
            );
        }

        private void ResolveThumbPathsForTab(int tabIndex) =>
            ThumbPathHelper.ResolveThumbPathsForTab(MainVM.MovieRecs, _thumbLayoutCache, tabIndex);

        private void Tabs_SelectionChangedAsync(object sender, SelectionChangedEventArgs e)
        {
            if (sender as TabControl != null && e.OriginalSource is TabControl)
            {
                var tabControl = sender as TabControl;
                int index = tabControl.SelectedIndex;
                if (index == -1) return;

                MainVM.DbInfo.CurrentTabIndex = index;

                if (!filterList.Any()) return;

                object[] listControls = [
                    SmallList,
                    BigList,
                    GridList,
                    ListDataGrid,
                    BigList10
                ];

                if (index >= 0 && index < listControls.Length)
                {
                    ResolveThumbPathsForTab(index);

                    if (listControls[index] is ItemsControl itemsControl)
                    {
                        itemsControl.ItemsSource = filterList;
                    }

                    StartTabSwitchThumbnailJob(index);
                    SelectFirstItem();
                }
            }
        }

        private void TagCopy_Click(object sender, RoutedEventArgs e)
        {
            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            if (mv.Tags == null) { return; }
            if (mv.Tags.Length == 0) { return; }

            Clipboard.SetData(DataFormats.Text, mv.Tags);
        }

        private void TagPaste_Click(object sender, RoutedEventArgs e)
        {
            if (!Clipboard.ContainsText(TextDataFormat.Text)) { return; }

            List<MovieRecords> mv;
            mv = GetSelectedItemsByTabIndex();
            if (mv == null) { return; }

            foreach (var rec in mv)
            {
                TagMutationService.ApplyPaste(rec, Clipboard.GetText(TextDataFormat.Text));
                UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "tag", rec.Tags);
            }

            Refresh();
        }

        private void TagAdd_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords dt = new();
            var tagEditWindow = new TagEdit
            {
                Title = "選択全ファイルにタグを追加",
                Owner = this,
                DataContext = dt,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            tagEditWindow.ShowDialog();

            if (tagEditWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            List<MovieRecords> mv;
            mv = GetSelectedItemsByTabIndex();
            if (mv == null) { return; }

            var dataContext = tagEditWindow.DataContext as MovieRecords;
            //リスト状態のタグと、改行付のタグを作る所
            var addedTags = dataContext.Tags;

            foreach (var rec in mv)
            {
                TagMutationService.ApplyAdd(rec, addedTags);
                UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "tag", rec.Tags);
            }
            Refresh();
        }

        private void TagDelete_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mvSelected = GetSelectedItemByTabIndex();
            if (mvSelected == null) { return; }

            var tagEditWindow = new TagEdit
            {
                Title = "選択全ファイルからタグを削除",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = mvSelected
            };
            tagEditWindow.ShowDialog();

            if (tagEditWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            List<MovieRecords> mv;
            mv = GetSelectedItemsByTabIndex();
            if (mv == null) { return; }

            var dataContext = tagEditWindow.DataContext as MovieRecords;
            //リスト状態のタグと、改行付のタグを作る所
            var tagsEditedWithNewLine = dataContext.Tags;

            foreach (var rec in mv)
            {
                TagMutationService.ApplyDelete(rec, tagsEditedWithNewLine);
                if (!string.IsNullOrEmpty(tagsEditedWithNewLine))
                {
                    UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "tag", rec.Tags);
                }
            }

            Refresh();
        }

        private void TagEdit_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            var tagEditWindow = new TagEdit
            {
                Title = "タグ編集",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = mv
            };
            tagEditWindow.ShowDialog();

            if (tagEditWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            var dc = tagEditWindow.DataContext as MovieRecords;
            TagMutationService.ApplyEdit(mv, dc.Tags);

            //DBのタグを更新する。
            UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, mv.Movie_Id, "tag", mv.Tags);

            Refresh();
        }

        private void MenuCopyAndMove_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            if (!(item.Name is "FileCopy" or "FileMove"))
            {
                return;
            }

            var dlgTitle = item.Name == "FileCopy" ? "コピー先の選択" : "移動先の選択";
            var dlg = new OpenFolderDialog
            {
                Title = dlgTitle,
                Multiselect = false,
                AddToRecent = true
            };

            var ret = dlg.ShowDialog();

            if (ret == true)
            {
                if (Tabs.SelectedItem == null) { return; }

                List<MovieRecords> mv;
                mv = GetSelectedItemsByTabIndex();
                if (mv == null) { return; }

                var destFolder = dlg.FolderName;
                foreach (var watcher in _fileWatcherManager.Watchers)
                {
                    if (watcher.Path == destFolder)
                    {
                        watcher.EnableRaisingEvents = false;
                    }
                }

                foreach (var rec in mv)
                {
                    var destName = Path.Combine(dlg.FolderName, Path.GetFileName(rec.Movie_Path));


                    if (item.Name == "FileCopy")
                    {
                        File.Copy(rec.Movie_Path, destName, true);
                    }
                    else
                    {
                        File.Move(rec.Movie_Path, destName, true);
                        rec.Movie_Path = destName;
                        rec.Dir = destFolder;
                        UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "movie_path", destName);
                        Refresh();
                    }

                }

                foreach (var watcher in _fileWatcherManager.Watchers)
                {
                    if (watcher.Path == destFolder)
                    {
                        watcher.EnableRaisingEvents = true;
                    }
                }
            }
        }

        private void MenuScore_Click(object sender, RoutedEventArgs e)
        {
            string keyName = "";
            if (sender is not MenuItem menuItem)
            {
                if (e is KeyEventArgs key)
                {
                    keyName = key.Key.ToString();
                }
            }
            else
            {
                keyName = menuItem.Name;
            }

            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            if (keyName.ToLower() is "add" or "scoreplus")
            {
                mv.Score += 1;
            }
            else if (keyName.ToLower() is "subtract" or "scoreminus")
            {
                mv.Score -= 1;
            }

            //DBのスコアを更新する。
            UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, mv.Movie_Id, "score", mv.Score);
        }

        private void OpenParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            if (Path.Exists(mv.Movie_Path))
            {
                if (Path.Exists(mv.Dir))
                {
                    Process.Start("explorer.exe", $"/select,{mv.Movie_Path}");
                }
            }
        }

        private void RenameFile_Click(object sender, RoutedEventArgs e)
        {
            string keyName = "";
            if (sender is not MenuItem menuItem)
            {
                if (e is KeyEventArgs keyEvent)
                {
                    keyName = keyEvent.Key.ToString();
                }
            }
            else
            {
                keyName = menuItem.Name;
            }

            if (!(keyName.ToLower() is "f2" or "renamefile"))
            {
                return;
            }

            if (Tabs.SelectedItem == null) { return; }
            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            //mv送っちゃうと、エクステンションの詳細も連動するのよね。当たり前だけど。
            //なので地味に使うところだけコピー。
            var body = Path.GetFileNameWithoutExtension(mv.Movie_Path);
            MovieRecords dt = new()
            {
                Movie_Id = mv.Movie_Id,
                Movie_Body = body,
                Movie_Path = mv.Movie_Path,
                Movie_Name = mv.Movie_Name,
                Ext = mv.Ext
            };

            var renameWindow = new RenameFile
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = dt
            };
            renameWindow.ShowDialog();

            if (renameWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            if (dt.Movie_Body == mv.Movie_Body && dt.Ext == mv.Ext)
            {
                return;
            }

            //リネーム。
            var checkFileName = mv.Movie_Body;
            var newFilePath = dt.Movie_Body;
            var checkExt = mv.Ext;
            var newExt = dt.Ext;

            //実態ファイルのリネームと新旧ファイルパス作成
            FileInfo mvFile = new(mv.Movie_Path);
            var destMoveFile = mv.Movie_Path.Replace(checkFileName, newFilePath);
            var destFolder = Path.GetDirectoryName(destMoveFile);
            destMoveFile = destMoveFile.Replace(checkExt, newExt);
            try
            {
                mvFile.MoveTo(destMoveFile, true);
            }
            catch (System.IO.IOException ex)
            {
                MessageBox.Show($"ファイルのリネームに失敗しました。\n{ex.Message}", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            //監視の一時停止（あれば）
            foreach (var watcher in _fileWatcherManager.Watchers)
            {
                if (watcher.Path == destFolder)
                {
                    watcher.EnableRaisingEvents = false;
                }
            }

            //監視時のリネーム処理の実態を呼び出す。
            RenameThumb(destMoveFile, mv.Movie_Path);

            //監視の再開（あれば）
            foreach (var watcher in _fileWatcherManager.Watchers)
            {
                if (watcher.Path == destFolder)
                {
                    watcher.EnableRaisingEvents = true;
                }
            }

            //stack : ここでもやっぱりエクステンションの詳細名称が追従しない。
            //タブの中をクリックしたとき、最後にデータをセットしてるんだけども、
            //ListViewのSelectedIndexを再設定してデータ入れても更新されなかったんだよねぇ。
        }

        private async void DeleteMovieRecord_Click(object sender, RoutedEventArgs e)
        {
            string keyName = "";
            if (sender is not MenuItem menuItem)
            {
                if (e is KeyEventArgs keyEvent)
                {
                    keyName = keyEvent.Key.ToString();
                }
            }
            else
            {
                keyName = menuItem.Name;
            }

            if (!(keyName.ToLower() is "delete" or "deletemovie" or "deletefile"))
            {
                return;
            }

            if (Tabs.SelectedItem == null) { return; }

            List<MovieRecords> mv;
            mv = GetSelectedItemsByTabIndex();
            if (mv == null) { return; }

            string msg = $"登録からデータを削除します\n（監視対象の場合、再監視で復活します）";
            string title = "登録から削除します";
            string radio1Content = "";
            string radio2Content = "";
            bool useRadio = false;

            if (keyName.Equals("deletefile", StringComparison.CurrentCultureIgnoreCase))
            {
                msg = "登録元のファイルを削除します。";
                title = "ファイル削除";
                useRadio = true;
                radio1Content = "ゴミ箱に移動して削除";
                radio2Content = "ディスクから完全に削除";
            }

            var dialogWindow = new MessageBoxEx(this)
            {
                CheckBoxContent = "サムネイルも削除する",
                UseRadioButton = useRadio,
                UseCheckBox = true,
                CheckBoxIsChecked = true,
                DlogMessage = msg,
                DlogTitle = title,
                Radio1Content = radio1Content,
                Radio2Content = radio2Content,
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.ExclamationBold
            };

            dialogWindow.ShowDialog();
            if (dialogWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            foreach (var rec in mv)
            {
                if (dialogWindow.checkBox.IsChecked == true)
                {
                    ThumbnailDeletionHelper.DeleteThumbnailsForRecord(
                        MainVM.DbInfo.ThumbFolder,
                        MainVM.DbInfo.DBName,
                        rec.Movie_Body,
                        rec.Hash);
                }
                DeleteMovieTable(MainVM.DbInfo.DBFullPath, rec.Movie_Id);

                //実ファイルの削除、2パターン
                if (keyName.Equals("deletefile", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (dialogWindow.radioButton1.IsChecked == true)
                    {
                        //ゴミ箱送り。
                        FileSystem.DeleteFile(rec.Movie_Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        //実削除
                        File.Delete(rec.Movie_Path);
                    }
                }

            }

            await FilterAndSortAsync(MainVM.DbInfo.Sort, true).ConfigureAwait(true);
        }

        private void BtnReCreateThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                MessageBox.Show("管理ファイルが選択されていません。", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (Tabs.SelectedItem == null) { return; }

            var dialogWindow = new MessageBoxEx(this)
            {
                DlogTitle = "サムネイルの再作成",
                DlogMessage = $"サムネイルを再作成します。よろしいですか？",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.EventQuestion
            };

            dialogWindow.ShowDialog();
            if (dialogWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            MenuToggleButton.IsChecked = false;
            List<QueueObj> thumbQueue = [.. MainVM.MovieRecs.Select(item => new QueueObj
            {
                MovieId = item.Movie_Id,
                MovieFullPath = item.Movie_Path,
                Tabindex = Tabs.SelectedIndex
            })];
            EnqueueThumbnailWork(thumbQueue, Tabs.SelectedIndex, beginNewJob: true);
        }

        private void BeginRefreshAllFileInfoFromMenu()
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                MessageBox.Show("管理ファイルが選択されていません。", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (!SinkuMetadataFetcher.IsAvailable)
            {
                return;
            }

            var dialogWindow = new MessageBoxEx(this)
            {
                DlogTitle = "ファイル情報の再取得",
                DlogMessage = "全ファイルの情報を再取得します。よろしいですか？",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.EventQuestion
            };

            dialogWindow.ShowDialog();
            if (dialogWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            MenuToggleButton.IsChecked = false;
            _ = RefreshAllFileInfoAsync();
        }

        private async void RefreshFileInfo_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            if (!SinkuMetadataFetcher.IsAvailable)
            {
                return;
            }

            List<MovieRecords> targets = GetSelectedItemsByTabIndex();
            if (targets == null || targets.Count == 0)
            {
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            await Task.Run(() =>
            {
                foreach (MovieRecords rec in targets)
                {
                    RefreshFileInfoCore(dbPath, rec);
                }
            }).ConfigureAwait(true);
        }

        private async Task RefreshAllFileInfoAsync()
        {
            if (Interlocked.CompareExchange(ref _fileInfoRefreshRunning, 1, 0) != 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath) || !SinkuMetadataFetcher.IsAvailable)
            {
                Interlocked.Exchange(ref _fileInfoRefreshRunning, 0);
                return;
            }

            List<MovieRecords> targets = [.. MainVM.MovieRecs];
            if (targets.Count == 0)
            {
                Interlocked.Exchange(ref _fileInfoRefreshRunning, 0);
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            FileInfoProgressSession session = null;

            try
            {
                session = await Dispatcher.InvokeAsync(() => new FileInfoProgressSession(targets.Count));
                CancellationToken cancelToken = session.Cancel;
                int done = 0;

                await Task.Run(() =>
                {
                    foreach (MovieRecords rec in targets)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        RefreshFileInfoCore(dbPath, rec);
                        done++;
                        int reportDone = done;
                        string reportPath = rec.Movie_Path;
                        _ = Dispatcher.InvokeAsync(
                            () => session.Report(reportDone, reportPath),
                            DispatcherPriority.Background);
                    }
                }, cancelToken).ConfigureAwait(true);

                await Dispatcher.InvokeAsync(() => session.Report(targets.Count, "完了"));
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (session != null)
                {
                    await Dispatcher.InvokeAsync(() => session.Dispose());
                }

                Interlocked.Exchange(ref _fileInfoRefreshRunning, 0);
            }
        }

        private void RefreshFileInfoCore(string dbPath, MovieRecords rec) =>
            FileInfoRefreshService.RefreshCore(dbPath, rec, action => RunOnUi(action));

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                RestoreDirectory = true,
                Filter = "設定ファイル(*.wb)|*.wb|すべてのファイル(*.*)|*.*",
                FilterIndex = 1,
                Title = "設定ファイル(.wb）の選択",
                OverwritePrompt = false
            };

            var result = sfd.ShowDialog();
            if (result == true)
            {
                if (Path.Exists(sfd.FileName))
                {
                    MessageBox.Show($"{sfd.FileName}は既に存在します。", "新規作成", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                MenuToggleButton.IsChecked = false;
                CreateDatabase(sfd.FileName);
                ReStackRecentTree(sfd.FileName);
                OpenDatafile(sfd.FileName);
                Properties.Settings.Default.LastDoc = sfd.FileName;
                Properties.Settings.Default.Save();
            }
        }

        private void ReStackRecentTree(string newItem)
        {
            recentFiles = RecentFilesService.ReStack(
                recentFiles,
                newItem,
                Properties.Settings.Default.RecentFilesCount);
            RecentFilesService.RebuildTreeChildren(MainVM.RecentTreeRoot, recentFiles);
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = Directory.GetCurrentDirectory(),
                RestoreDirectory = true,
                Filter = "設定ファイル(*.wb)|*.wb|すべてのファイル(*.*)|*.*",
                FilterIndex = 1,
                Multiselect = false,
                Title = "設定ファイル(.wb）の選択"
            };

            MenuToggleButton.IsChecked = false;

            var result = ofd.ShowDialog();

            if (result == true)
            {
                ReStackRecentTree(ofd.FileName);
                Properties.Settings.Default.LastDoc = ofd.FileName;
                Properties.Settings.Default.Save();
                OpenDatafile(ofd.FileName);
            }
        }

        //
        //
        //
        //
        //テストボタン。色々使う。
        //
        //
        //
        //
        private async void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            // フォルダの最新状態をDBに反映
            //await CheckFolderAsync(CheckMode.Auto);

            // ブックマーク・リスト等の再取得
            GetBookmarkTable();
            BookmarkList.Items.Refresh();
            await FilterAndSortAsync(MainVM.DbInfo.Sort, true).ConfigureAwait(true);
            Refresh();
        }

        private void MenuBtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button item)
            {
                if (!string.IsNullOrEmpty(item.Tag.ToString()))
                {
                    var tag = item.Tag.ToString();
                    if (tag != NavigationMenuIds.SettingsRoot)
                    {

                        switch (tag)
                        {
                            case NavigationMenuIds.CommonSettings:
                                MenuToggleButton.IsChecked = false;
                                var CommonSettingsWindow = new CommonSettingsWindow
                                {
                                    Owner = this,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                };
                                CommonSettingsWindow.ShowDialog();
                                break;
                            case NavigationMenuIds.DatabaseSettings:
                                if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                                {
                                    MessageBox.Show("管理ファイルが選択されていません。", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                                    return;
                                }

                                MenuToggleButton.IsChecked = false;
                                var sysData = new DatabaseSettings(MainVM.DbInfo.DBFullPath);
                                var settingsWindow = new SettingsWindow
                                {
                                    Owner = this,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                    DataContext = sysData
                                };
                                settingsWindow.ShowDialog();

                                UpsertSystemTable(MainVM.DbInfo.DBFullPath, "thum", settingsWindow.ThumbFolder.Text);
                                UpsertSystemTable(MainVM.DbInfo.DBFullPath, "bookmark", settingsWindow.BookmarkFolder.Text);
                                UpsertSystemTable(MainVM.DbInfo.DBFullPath, "keepHistory", settingsWindow.KeepHistory.Text);
                                UpsertSystemTable(MainVM.DbInfo.DBFullPath, "playerPrg", settingsWindow.PlayerPrg.Text);
                                var param = settingsWindow.PlayerParam.Text == null ? "" : settingsWindow.PlayerParam.Text.ToString();
                                UpsertSystemTable(MainVM.DbInfo.DBFullPath, "playerParam", param);

                                GetSystemTable(MainVM.DbInfo.DBFullPath);
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        if (MenuConfig.Items.Count > 0)
                        {
                            if (MenuConfig.Items[0] is TreeSource topNode)
                            {
                                topNode.IsExpanded = !topNode.IsExpanded;
                            }
                        }
                    }
                }
            }
        }

        private void MenuBtnTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button item)
            {
                if (!string.IsNullOrEmpty(item.Tag.ToString()))
                {
                    var tag = item.Tag.ToString();
                    if (tag != NavigationMenuIds.ToolsRoot)
                    {
                        if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                        {
                            MessageBox.Show("管理ファイルが選択されていません。", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }

                        MenuToggleButton.IsChecked = false;

                        switch (tag)
                        {
                            case NavigationMenuIds.WatchFolderEdit:
                                var watchWindow = new WatchWindow(MainVM.DbInfo.DBFullPath)
                                {
                                    Owner = this,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                };
                                watchWindow.ShowDialog();
                                break;

                            case NavigationMenuIds.WatchFolderCheck:
                                _ = CheckFolderAsync(FolderCheckMode.Manual);
                                break;

                            case NavigationMenuIds.RecreateAllThumbnails:
                                if (Tabs.SelectedItem == null) { return; }

                                var dialogWindow = new MessageBoxEx(this)
                                {
                                    DlogTitle = "サムネイルの再作成",
                                    DlogMessage = $"サムネイルを再作成します。よろしいですか？",
                                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.EventQuestion
                                };

                                dialogWindow.ShowDialog();
                                if (dialogWindow.CloseStatus() == MessageBoxResult.Cancel)
                                {
                                    return;
                                }

                                List<QueueObj> thumbQueue = [.. MainVM.MovieRecs.Select(rec => new QueueObj
                                {
                                    MovieId = rec.Movie_Id,
                                    MovieFullPath = rec.Movie_Path,
                                    Tabindex = Tabs.SelectedIndex
                                })];
                                EnqueueThumbnailWork(thumbQueue, Tabs.SelectedIndex, beginNewJob: true);
                                break;

                            case NavigationMenuIds.RefreshAllFileInfo:
                                BeginRefreshAllFileInfoFromMenu();
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        if (MenuTool.Items.Count > 0)
                        {
                            if (MenuTool.Items[0] is TreeSource topNode)
                            {
                                topNode.IsExpanded = !topNode.IsExpanded;
                            }
                        }
                    }
                }
            }
        }

        private void MenuRecentTree_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button item)
            {
                if (!string.IsNullOrEmpty(item.Tag.ToString()))
                {
                    var tag = item.Tag.ToString();
                    if (tag != RECENT_OPEN_FILE_LABEL)
                    {
                        MenuToggleButton.IsChecked = false;
                        if (!string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                        {
                            UpdateSkin();
                            UpdateSort();
                        }
                        ReStackRecentTree(tag);
                        OpenDatafile(tag);
                        Properties.Settings.Default.LastDoc = tag;
                        Properties.Settings.Default.Save();
                    }
                    else
                    {
                        if (MenuRecent.Items.Count > 0)
                        {
                            if (MenuRecent.Items[0] is TreeSource topNode)
                            {
                                topNode.IsExpanded = !topNode.IsExpanded;
                            }
                        }
                    }
                }
            }
        }

        public async void PlayMovie_Click(object sender, RoutedEventArgs e)
        {
            int msec = 0;
            int secPos = 0;
            string moviePath = "";
            MovieRecords mv = new();
            bool notBookmark = true;

            if (sender is Label labelObj && labelObj.Name == "LabelBookMark")
            {
                var item = (Label)sender;
                if (item != null)
                {
                    notBookmark = false;
                    mv = item.DataContext as MovieRecords;
                    MovieRecords bookmarkedMv = MainVM.MovieRecs.Where(
                            x => x.Movie_Name.Contains(mv.Movie_Body, StringComparison.CurrentCultureIgnoreCase)).First();
                    string bookMarkedFilePath = bookmarkedMv.Movie_Path;
                    MovieInfo mvi = new(bookMarkedFilePath, true);
                    msec = (int)mv.Score / (int)mvi.FPS * 1000;
                    moviePath = $"\"{bookMarkedFilePath}\"";
                    UpdateBookmarkViewCount(MainVM.DbInfo.DBFullPath, mv.Movie_Id);
                }
            }

            if (notBookmark)
            {
                if (Tabs.SelectedItem == null) { return; }

                mv = GetSelectedItemByTabIndex();
                if (mv == null) { return; }

                moviePath = $"\"{mv.Movie_Path}\"";

                if (!Path.Exists(mv.Movie_Path))
                {
                    return;
                }

                if (sender is MenuItem senderObj && senderObj.Name == "PlayFromThumb")
                {
                    msec = GetPlayPosition(Tabs.SelectedIndex, mv, ref secPos);
                }
            }

            ExternalPlayerLaunchRequest request = ExternalPlayerLauncher.BuildRequest(
                SelectSystemTable("playerPrg"),
                SelectSystemTable("playerParam"),
                Properties.Settings.Default.DefaultPlayerPath,
                Properties.Settings.Default.DefaultPlayerParam,
                mv,
                moviePath,
                msec);

            try
            {
                await ExternalPlayerLauncher.LaunchAsync(request, this).ConfigureAwait(true);
                ExternalPlayerLauncher.ApplyPlaybackStats(mv, MainVM.DbInfo.DBFullPath);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_isDeletingSearchHistory) { return; }
            if (_isApplyingSearchKeyword) { return; }

            // ドロップダウンが開いている間に選択が変わった場合のみフラグを立てる
            if (SearchBox.IsDropDownOpen)
            {
                _searchBoxItemSelectedByUser = true;
            }

            if (e.Source is ComboBox)
            {
                /*
                FilterAndSort(MainVM.DbInfo.Sort);  //サーチのコンボチェンジイベント。
                SelectFirstItem();
                if (!string.IsNullOrEmpty(MainVM.DbInfo.SearchKeyword))
                {
                    //セレクションが変わってもHistoryに書いてるかも。
                    InsertHistoryTable(MainVM.DbInfo.DBFullPath, MainVM.DbInfo.SearchKeyword);
                }
                */
            }
        }

        private void SearchBoxItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is ComboBoxItem item && item.IsMouseOver)
            {
                item.IsSelected = true;
            }
        }

        private async void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_isApplyingSearchKeyword) { return; }

            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            if (!string.IsNullOrEmpty(MainVM.DbInfo.SearchKeyword))
            {
                string dbPath = MainVM.DbInfo.DBFullPath;
                string keyword = MainVM.DbInfo.SearchKeyword;

                // LostFocus での同期DBアクセスがUI停止を起こしやすいのでバックグラウンド化。
                await Task.Run(() =>
                {
                    InsertFindFactTable(dbPath, keyword);
                    InsertHistoryTable(dbPath, keyword);
                }).ConfigureAwait(true);
                //GetHistoryTable(MainVM.DbInfo.DBFullPath);
            }
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_imeFlag) { return; }
            if (_isDeletingSearchHistory) { return; }
            if (_isApplyingSearchKeyword) { return; }

            if (e.Source is ComboBox combo)
            {
                var text = combo.Text;
                /* インクリメントサーチ部。一旦コメントアウト。
                // 入力文字列の末尾が -, |, { のいずれかならサーチしない。}は終了なので、サーチスタート。
                if (!string.IsNullOrEmpty(text))
                {
                    // すでに{があり、}がまだ無い場合はreturn
                    int openIdx = text.IndexOf('{');
                    int closeIdx = text.IndexOf('}');
                    if (openIdx >= 0 && (closeIdx < 0 || closeIdx < openIdx))
                    {
                        return;
                    }

                    char lastChar = text[^1];
                    if (lastChar == '-' || lastChar == '|' || lastChar == '{')
                    {
                        return;
                    }
                }
                //インクリメンタルサーチがなぁ。ちょっと間隔で調整的な。美しくない。
                DateTime now = DateTime.Now;
                TimeSpan timeSinceLastUpdate = now - _lastInputTime;

                if (timeSinceLastUpdate >= _timeInputInterval)
                {
                    _lastInputTime = now;
                    FilterAndSort(MainVM.DbInfo.Sort);  //サーチのテキストチェンジイベント。
                    SelectFirstItem();
                }
                */
                if (string.IsNullOrEmpty(text))
                {
                    // テキストが空の場合はメモリ上の一覧を再フィルタするだけ（DB再読込しない）
                    MainVM.DbInfo.SearchKeyword = "";
                    await FilterAndSortAsync(MainVM.DbInfo.Sort, false).ConfigureAwait(true);
                    SelectFirstItem();
                }
            }
        }

        // ドロップダウンリストでマウス選択時
        // DropDownClosedで、ユーザー操作による選択時のみ検索
        private void SearchBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_isDeletingSearchHistory)
            {
                return;
            }

            if (_searchBoxItemSelectedByUser)
            {
                DoSearchBoxSearch();
                _searchBoxItemSelectedByUser = false;
                //_searchBoxItemSelectedByMouse = false;
            }
        }

        // ドロップダウンリスト内でマウスクリック時にフラグを立てる
        private void SearchBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //_searchBoxItemSelectedByMouse = true;
            _searchBoxItemSelectedByUser = true;
        }

        private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_imeFlag) { return; }
            if (e.Source is ComboBox combo)
            {
                // Deleteキーで履歴削除
                if (e.Key == Key.Delete && combo.IsDropDownOpen && combo.SelectedItem is History selectedHistory)
                {
                    e.Handled = true;

                    string keepText = combo.Text;
                    long findId = selectedHistory.Find_Id;

                    _isDeletingSearchHistory = true;
                    try
                    {
                        MainVM.HistoryRecs.Remove(selectedHistory);
                        combo.SelectedIndex = -1;
                        combo.Text = keepText;
                        if (!string.Equals(MainVM.DbInfo.SearchKeyword, keepText, StringComparison.Ordinal))
                        {
                            MainVM.DbInfo.SearchKeyword = keepText;
                        }
                    }
                    finally
                    {
                        _isDeletingSearchHistory = false;
                    }

                    _ = Task.Run(() => DeleteHistoryTable(MainVM.DbInfo.DBFullPath, findId));
                    return;
                }

                // Enterで検索
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    _searchBoxItemSelectedByUser = false;
                    await SearchByKeywordAsync(combo.Text ?? "").ConfigureAwait(true);
                    return;
                }
            }
        }

        // 検索実行処理
        public async Task SearchByKeywordAsync(string keyword)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            string text = keyword ?? "";
            _isApplyingSearchKeyword = true;
            try
            {
                // 履歴リストを先に更新してから ComboBox へ反映しないと、
                // SelectedValue 不一致で Text が空になり全件表示へ戻ることがある。
                if (!string.IsNullOrEmpty(text))
                {
                    PromoteSearchHistory(text);
                }

                MainVM.DbInfo.SearchKeyword = text;
                SearchBox.Text = text;
                await FilterAndSortAsync(MainVM.DbInfo.Sort, false).ConfigureAwait(true);
                SelectFirstItem();
                SearchBox.Focus();
            }
            finally
            {
                _isApplyingSearchKeyword = false;
            }

            if (!string.IsNullOrEmpty(text)
                && !string.Equals(SearchBox.Text, text, StringComparison.Ordinal))
            {
                _isApplyingSearchKeyword = true;
                try
                {
                    MainVM.DbInfo.SearchKeyword = text;
                    SearchBox.Text = text;
                }
                finally
                {
                    _isApplyingSearchKeyword = false;
                }
            }

            // 特殊検索（例: {notag}）も通常検索と同様に履歴へ反映する。
            if (!string.IsNullOrEmpty(text))
            {
                string dbPath = MainVM.DbInfo.DBFullPath;
                _ = Task.Run(() => InsertHistoryTable(dbPath, text));
            }
        }

        private async void DoSearchBoxSearch()
        {
            await SearchByKeywordAsync(SearchBox.Text).ConfigureAwait(true);
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null)
            {
                viewExtDetail.Visibility = Visibility.Collapsed;
                return;
            }
            viewExtDetail.DataContext = mv;
            viewExtDetail.Visibility = Visibility.Visible;

            string detailThumbFile = ThumbnailLayoutCache.GetThumbFileName(
                Path.GetFileNameWithoutExtension(mv.Movie_Name),
                mv.Hash
            );
            mv.ThumbDetail = _thumbLayoutCache.BuildThumbPath(99, detailThumbFile, checkExists: true);

            if (mv.ThumbDetail.Contains("error", StringComparison.CurrentCultureIgnoreCase)
                && !_thumbnailScheduler.JobCoordinator.IsTracked(mv.Movie_Id, 99))
            {
                EnqueueSilentThumbnailWork(new QueueObj
                {
                    MovieId = mv.Movie_Id,
                    MovieFullPath = mv.Movie_Path,
                    Tabindex = 99,
                });
            }
        }

        private void ListDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            e.Cancel = true;
        }

        private int GetPlayPosition(int tabIndex, MovieRecords mv, ref int returnPos) =>
            PlayPositionResolver.GetPlayPositionMsec(lbClickPoint, tabIndex, mv, ref returnPos);

        public MovieRecords GetSelectedItemByTabIndex() => TabSelectionHelper.GetSelectedItem(this);

        private List<MovieRecords> GetSelectedItemsByTabIndex() => TabSelectionHelper.GetSelectedItems(this);

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // senderがLabelで、DataContextがMovieRecordsであることを確認
            if (sender is Label label && label.DataContext is MovieRecords record)
            {
                // DataGridの選択状態を強制的にセット
                ListDataGrid.SelectedItem = record;
                lbClickPoint = e.GetPosition(label);
            }
        }

        private void Tab_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Tabs.SelectedIndex == -1) { return; }
            if (Tabs.SelectedItem == null) { return; }

            switch (e.Key)
            {
                case Key.Enter:                         //再生
                    PlayMovie_Click(sender, e); break;
                case Key.F6:                            //タグ編集
                    TagEdit_Click(sender, e); break;
                case Key.C:                             //タグのコピー
                    TagCopy_Click(sender, e);
                    break;
                case Key.V:                             //タグの貼り付け
                    TagPaste_Click(sender, e);
                    break;
                case Key.Add:                           //スコアプラス
                case Key.Subtract:                      //スコアマイナス
                    MenuScore_Click(sender, e);
                    break;
                case Key.Delete:                        //登録の削除
                    DeleteMovieRecord_Click(sender, e);
                    break;
                case Key.F2:                            //名前の変更
                    RenameFile_Click(sender, e);
                    break;
                case Key.F12:                           //親フォルダ
                    OpenParentFolder_Click(sender, e);
                    break;
                case Key.P:                             //プロパティ
                    break;
                default:
                    return;
            }
        }

        private async void ComboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (sender is ComboBox senderObj)
            {
                if (MainVM.MovieRecs.Count > 0)
                {
                    if (senderObj.SelectedValue != null)
                    {
                        var id = senderObj.SelectedValue.ToString();
                        MainVM.DbInfo.Sort = id;
                        await ApplyFilterAndSortAsync(id).ConfigureAwait(true);
                        SelectFirstItem();
                    }
                }
            }
        }

        // SmallListのアイテム内要素クリック時に選択状態にするイベントハンドラ
        private void SmallListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                // Ctrlキー押下時は選択状態を変更しない（WPF標準の複数選択動作に任せる）
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    return;

                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    SmallList.SelectedItem = item.DataContext;
                }
            }
        }

        // BigListのアイテム内要素クリック時に選択状態にするイベントハンドラ
        private void BigListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    return;

                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    BigList.SelectedItem = item.DataContext;
                }
            }
        }

        // BigList10のアイテム内要素クリック時に選択状態にするイベントハンドラ
        private void BigList10Item_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    return;

                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    BigList10.SelectedItem = item.DataContext;
                }
            }
        }

        private void CreateWatcher()
        {
            string sql = $"SELECT * FROM watch where watch = 1";
            GetWatchTable(MainVM.DbInfo.DBFullPath, sql);

            foreach (DataRow row in watchData.Rows)
            {
                //存在しない監視フォルダは読み飛ばし。
                if (!Path.Exists(row["dir"].ToString())) { continue; }
                string checkFolder = row["dir"].ToString();
                bool sub = (long)row["sub"] == 1;

                RunWatcher(checkFolder, sub);
            }
        }

        private Task ReportFolderCheckProgressAsync(FolderCheckProgressSession session, int done, string detail)
        {
            if (session == null)
            {
                return Task.CompletedTask;
            }

            if (Dispatcher.CheckAccess())
            {
                session.Report(done, detail);
                return Task.CompletedTask;
            }

            return Dispatcher.InvokeAsync(() => session.Report(done, detail)).Task;
        }

        private Task<FolderCheckProgressSession> BeginFolderCheckProgressAsync(int totalFolders)
        {
            if (totalFolders < 1)
            {
                return Task.FromResult<FolderCheckProgressSession>(null);
            }

            if (Dispatcher.CheckAccess())
            {
                return Task.FromResult(new FolderCheckProgressSession(totalFolders));
            }

            return Dispatcher.InvokeAsync(() => new FolderCheckProgressSession(totalFolders)).Task;
        }

        private Task EndFolderCheckProgressAsync(FolderCheckProgressSession session)
        {
            if (session == null)
            {
                return Task.CompletedTask;
            }

            if (Dispatcher.CheckAccess())
            {
                session.Complete();
                return Task.CompletedTask;
            }

            return Dispatcher.InvokeAsync(session.Complete, DispatcherPriority.Normal).Task;
        }

        /// <summary>
        /// 起動時と手動時のフォルダチェック。
        /// DB内レコードとフォルダ内対象ファイルの差分比較し、差分があれば追加。
        /// リネームや削除には対応出来ず。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException"></exception>
        private async Task CheckFolderAsync(FolderCheckMode mode)
        {
            bool FolderCheckflg = false;
            List<QueueObj> addFiles = [];
            string checkExt = Properties.Settings.Default.CheckExt;

            GetWatchTable(MainVM.DbInfo.DBFullPath, FolderCheckService.GetWatchSql(mode));

            List<(string Folder, bool Sub)> foldersToCheck = FolderCheckService.GetFoldersToCheck(watchData);

            if (foldersToCheck.Count == 0)
            {
                return;
            }

            int totalFolders = foldersToCheck.Count;
            FolderCheckProgressSession folderCheckProgress = await BeginFolderCheckProgressAsync(totalFolders).ConfigureAwait(true);

            try
            {
                for (int folderIndex = 0; folderIndex < foldersToCheck.Count; folderIndex++)
                {
                    (string checkFolder, bool sub) = foldersToCheck[folderIndex];
                    await ReportFolderCheckProgressAsync(
                        folderCheckProgress,
                        folderIndex,
                        $"{checkFolder} 監視実施中…").ConfigureAwait(true);

                    var di = new DirectoryInfo(checkFolder);
                    EnumerationOptions enumOption = new()
                    {
                        RecurseSubdirectories = sub
                    };

                    try
                    {
                        IEnumerable<FileInfo> ssFiles = checkExt.Split(',').SelectMany(filter => di.EnumerateFiles(filter, enumOption));
                        bool isHit = false;
                        foreach (var ssFile in ssFiles)
                        {
                            bool existsInDb = FolderCheckService.IsFileRegistered(MainVM.MovieRecs, ssFile.FullName);
                            if (!existsInDb)
                            {
                                if (!isHit)
                                {
                                    await ReportFolderCheckProgressAsync(
                                        folderCheckProgress,
                                        folderIndex,
                                        $"{checkFolder} に更新あり。").ConfigureAwait(true);
                                    isHit = true;
                                }

                                MovieInfo mvi = new(ssFile.FullName);
                                await InsertMovieTable(MainVM.DbInfo.DBFullPath, mvi);

                                FolderCheckflg = true;

                                TabInfo tbi = new(MainVM.DbInfo.CurrentTabIndex, MainVM.DbInfo.DBName, MainVM.DbInfo.ThumbFolder);

                                var hash = mvi.Hash;
                                var fileBody = Path.GetFileNameWithoutExtension(mvi.MoviePath);
                                var saveThumbFileName = Path.Combine(tbi.OutPath, $"{fileBody}.#{hash}.jpg");

                                if (Path.Exists(saveThumbFileName))
                                {
                                    continue;
                                }

                                QueueObj temp = new()
                                {
                                    MovieId = mvi.MovieId,
                                    MovieFullPath = mvi.MoviePath,
                                    Tabindex = MainVM.DbInfo.CurrentTabIndex
                                };
                                addFiles.Add(temp);

                                DataTable dt = GetData(MainVM.DbInfo.DBFullPath, "select * from movie order by movie_id desc");
                                DataRowToViewData(dt.Rows[0]);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.GetType() == typeof(IOException))
                        {
                            await Task.Delay(1000);
                        }
                    }

                    await ReportFolderCheckProgressAsync(
                        folderCheckProgress,
                        folderIndex + 1,
                        $"{checkFolder} 監視完了").ConfigureAwait(true);
                    await Task.Delay(100);
                }
            }
            finally
            {
                await EndFolderCheckProgressAsync(folderCheckProgress).ConfigureAwait(true);
            }

            if (FolderCheckflg)
            {
                await FilterAndSortAsync(MainVM.DbInfo.Sort, true).ConfigureAwait(true);

                EnqueueThumbnailWork(addFiles, MainVM.DbInfo.CurrentTabIndex, beginNewJob: true);
            }
        }

        /// <summary>
        /// CheckThumbAsync サムネイル作成用に起動時にぶん投げるタスク。常時起動。終了条件はねぇ。
        /// </summary>
        private async Task CheckThumbAsync(CancellationToken cts = default)
        {
            try
            {
                await _thumbnailQueueProcessor
                    .RunAsync(
                        _thumbnailScheduler.Queue,
                        (queueObj, token) => CreateThumbAsync(queueObj, false, token),
                        _thumbnailScheduler.JobCoordinator,
                        _thumbnailScheduler.SyncRoot,
                        maxParallelism: GetThumbnailQueueMaxParallelism(),
                        maxParallelismResolver: GetThumbnailQueueMaxParallelism,
                        pollIntervalMs: 100,
                        cts: cts
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string s = string.Format($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} :");
                Debug.WriteLine($"{s} {e.Message} ");
            }
        }

        private async Task CreateBookmarkThumbAsync(string movieFullPath, string saveThumbPath, int capturePos)
        {
            await BookmarkThumbnailCreator.CreateAsync(movieFullPath, saveThumbPath, capturePos).ConfigureAwait(true);
            BookmarkList.Items.Refresh();
        }

        private ThumbnailCreationHost CreateThumbnailHost() => new()
        {
            DbFullPath = MainVM.DbInfo.DBFullPath,
            DbName = MainVM.DbInfo.DBName,
            ThumbFolder = MainVM.DbInfo.ThumbFolder,
            MovieRecords = MainVM.MovieRecs,
            LayoutCache = _thumbLayoutCache,
            RunOnUi = RunOnUi,
            ApplyThumbPathsOnUi = ApplyThumbPathsOnUi,
            ApplyFailurePlaceholder = ApplyThumbnailFailurePlaceholder,
            IsResizeThumb = Properties.Settings.Default.IsResizeThumb,
            UpdateMovieColumn = (dbPath, movieId, value) =>
                UpdateMovieSingleColumn(dbPath, movieId, "movie_length", value),
        };

        private Task CreateThumbAsync(QueueObj queueObj, bool isManual = false, CancellationToken cts = default) =>
            ThumbnailCreationOrchestrator.CreateAsync(CreateThumbnailHost(), queueObj, isManual, cts);

        private void ApplyThumbnailFailurePlaceholder(QueueObj queueObj, string saveThumbFileName)
        {
            if (!ThumbnailFailurePlaceholder.TryWrite(_thumbLayoutCache, queueObj.Tabindex, saveThumbFileName))
            {
                return;
            }

            ApplyThumbPathsOnUi(queueObj, saveThumbFileName);
        }

        private void ApplyThumbPathsOnUi(QueueObj queueObj, string saveThumbFileName) =>
            UiDispatcherHelper.RunOnUi(Dispatcher, () => ApplyThumbPaths(queueObj, saveThumbFileName));

        private void RunOnUi(Action action) => UiDispatcherHelper.RunOnUi(Dispatcher, action);

        private void ApplyThumbPaths(QueueObj queueObj, string saveThumbFileName) =>
            ThumbPathHelper.ApplyThumbPaths(MainVM.MovieRecs, queueObj, saveThumbFileName);

        /// <summary>
        /// 手動等間隔サムネイル作成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateThumb_EqualInterval(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            // 複数選択対応: 選択中の全アイテムを取得
            List<MovieRecords> selectedItems = GetSelectedItemsByTabIndex();
            if (selectedItems == null || selectedItems.Count == 0) { return; }

            List<QueueObj> thumbQueue = [.. selectedItems.Select(mv => new QueueObj
            {
                MovieId = mv.Movie_Id,
                MovieFullPath = mv.Movie_Path,
                Tabindex = Tabs.SelectedIndex
            })];
            EnqueueThumbnailWork(thumbQueue, Tabs.SelectedIndex, beginNewJob: true);
        }

        #region マニュアルサムネイル用のプレイヤー関連

        private bool IsPlaying = false;
        /// <summary>
        /// 再生ボタンクリック時のイベントハンドラ
        /// パクリ元：https://resanaplaza.com/2023/06/24/%e3%80%90%e3%82%b5%e3%83%b3%e3%83%97%e3%83%ab%e6%ba%80%e8%bc%89%e3%80%91c%e3%81%a7%e5%8b%95%e7%94%bb%e5%86%8d%e7%94%9f%e3%81%97%e3%82%88%e3%81%86%e3%82%88%ef%bc%81%ef%bc%88mediaelement%ef%bc%89/
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!_manualPreview.IsOpen) { return; }

            PlayerArea.Visibility = Visibility.Visible;
            PlayerController.Visibility = Visibility.Visible;
            uxPreviewImage.Visibility = Visibility.Visible;
            IsPlaying = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            timer.Start();
        }

        /// <summary>
        /// 一時停止ボタンクリック時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
        }

        private void UxPreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_manualPreview.IsOpen) { return; }

            IsPlaying = !IsPlaying;
        }

        /// <summary>
        /// ストップボタンクリック時のイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            CloseManualThumbnailPreview();
        }

        /// <summary>
        /// タイムラインスライダーのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UxTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_manualPreview.IsOpen) { return; }

            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastUpdate = now - _lastSliderTime;

            if (timeSinceLastUpdate >= _timeSliderInterval)
            {
                _manualPreview.SetPositionMs(uxTimeSlider.Value);
                _lastSliderTime = now;
                uxTime.Text = _manualPreview.PositionText;
            }
        }

        /// <summary>
        /// ボリュームスライダーのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UxVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (uxVolume != null)
            {
                uxVolume.Text = ((int)(uxVolumeSlider.Value * 100)).ToString();
            }
        }

        /// <summary>
        /// キャプチャボタンのイベントハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Capture_Click(object sender, RoutedEventArgs e)
        {
            //QueueObj 作って、サムネ作成する。どのパネルか、秒数はどこか、差し替える画像はどれか。
            //その辺は、サムネ作成側の処理で判断。

            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            timer.Stop();
            IsPlaying = false;

            QueueObj queueObj = new()
            {
                MovieId = mv.Movie_Id,
                MovieFullPath = mv.Movie_Path,
                Tabindex = Tabs.SelectedIndex,
                ThumbPanelPos = manualPos,
                ThumbTimePos = _manualPreview.PositionSeconds
            };

            CloseManualThumbnailPreview();

            await Task.Delay(10);
            _ = CreateThumbAsync(queueObj, true);
        }

        public void DeleteBookmark(object sender, RoutedEventArgs e)
        {
            if (sender is Button deleteButton)
            {
                var item = deleteButton.DataContext as MovieRecords;
                DeleteBookmarkTable(MainVM.DbInfo.DBFullPath, item.Movie_Id);
                GetBookmarkTable();
                BookmarkList.Items.Refresh();
            }
        }

        private async void AddBookmark_Click(object sender, RoutedEventArgs e)
        {
            //QueueObj 作って、サムネ作成する。どのパネルか、秒数はどこか、差し替える画像はどれか。
            //その辺は、サムネ作成側の処理で判断。

            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            timer.Stop();
            IsPlaying = false;

            MovieInfo mvi = new(mv.Movie_Path, true);        //Hashの取得が重いのでオプション付けた。ブックマークには不要。

            int pos = _manualPreview.PositionSeconds;
            var targetFrame = pos * (int)mvi.FPS;
            var timestamp = string.Format($"{DateTime.Now:HH-mm-ss}");
            var thumbBody = $"{mv.Movie_Body}[({targetFrame}){timestamp}]";
            var thumbFileName = $"{thumbBody}.jpg";
            var thumbFolder = MainVM.DbInfo.BookmarkFolder;
            var defaultThumbFolder = Path.Combine(Directory.GetCurrentDirectory(), "bookmark", MainVM.DbInfo.DBName);
            thumbFolder = thumbFolder == "" ? defaultThumbFolder : thumbFolder;
            thumbFileName = Path.Combine(thumbFolder, thumbFileName);
            if (!Path.Exists(thumbFolder))
            {
                Directory.CreateDirectory(thumbFolder);
            }

            await Task.Delay(10);
            //bookmark用サムネイル作成処理。通常と重複は多いんだけども。
            _ = CreateBookmarkThumbAsync(mv.Movie_Path, thumbFileName, pos);

            CloseManualThumbnailPreview();

            //Bookmarkテーブルへのレコード書き込み処理追加
            mvi.MovieName = thumbBody;
            mvi.MoviePath = $"{thumbBody}.jpg";
            InsertBookmarkTable(MainVM.DbInfo.DBFullPath, mvi);
            GetBookmarkTable();
            BookmarkList.Items.Refresh();
        }

        private async void ManualThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            int msec = 0;
            if (sender is MenuItem senderObj)
            {
                if (senderObj.Name == "ManualThumbnail")
                {
                    msec = GetPlayPosition(Tabs.SelectedIndex, mv, ref manualPos);
                }
            }

            await _manualPreview.OpenAsync(mv.Movie_Path, msec);
            if (_manualPreview.DurationMs > 0d)
            {
                uxTimeSlider.Maximum = _manualPreview.DurationMs;
            }

            uxTimeSlider.Value = _manualPreview.PositionMs;
            uxTime.Text = _manualPreview.PositionText;
            IsPlaying = false;
            PlayerArea.Visibility = Visibility.Visible;
            uxPreviewImage.Visibility = Visibility.Visible;
            PlayerController.Visibility = Visibility.Visible;
            uxTimeSlider.Focus();

            timer.Start();
            await _manualPreview.RefreshPreviewAsync();
        }

        private void CloseManualThumbnailPreview()
        {
            timer.Stop();
            _manualPreview.CancelPending();
            _manualPreview.Close();
            PlayerArea.Visibility = Visibility.Collapsed;
            PlayerController.Visibility = Visibility.Collapsed;
            uxPreviewImage.Visibility = Visibility.Collapsed;
            uxPreviewImage.Source = null;
            IsPlaying = false;
        }

        private void FR_Click(object sender, RoutedEventArgs e)
        {
            var tempSlider = (int)uxTimeSlider.Value - 100;
            if (tempSlider < 0) { tempSlider = 0; }
            FF_FR(tempSlider);
        }
        private void FF_Click(object sender, RoutedEventArgs e)
        {
            var tempSlider = (int)uxTimeSlider.Value + 100;
            if (tempSlider > uxTimeSlider.Maximum) { tempSlider = (int)uxTimeSlider.Maximum; }
            FF_FR(tempSlider);
        }
        private void FF_FR(int tempSlider)
        {
            uxTimeSlider.Value = tempSlider;
            _manualPreview.SetPositionMs(tempSlider);
            uxTime.Text = _manualPreview.PositionText;
        }

        /// <summary>
        /// ドラッグしてなければ、再生中はスライダーを進めてプレビューを更新する。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (isDragging || !_manualPreview.IsOpen)
            {
                return;
            }

            if (!IsPlaying)
            {
                return;
            }

            double nextMs = _manualPreview.PositionMs + timer.Interval.TotalMilliseconds;
            if (_manualPreview.DurationMs > 0d && nextMs > _manualPreview.DurationMs)
            {
                nextMs = _manualPreview.DurationMs;
                IsPlaying = false;
            }

            _manualPreview.SetPositionMs(nextMs, schedulePreview: false);
            uxTimeSlider.Value = _manualPreview.PositionMs;
            uxTime.Text = _manualPreview.PositionText;
            _ = _manualPreview.RefreshPreviewAsync();
        }

        private void UxTimeSlider_DragEnter(object sender, DragEventArgs e)
        {
            isDragging = true;
        }

        private void UxTimeSlider_DragLeave(object sender, DragEventArgs e)
        {
            isDragging = false;
            _manualPreview.SetPositionMs(uxTimeSlider.Value);
            uxTime.Text = _manualPreview.PositionText;
        }

        #endregion

        ComboBox IMainWindowActions.SearchBox => SearchBox;
        TabControl IMainWindowActions.Tabs => Tabs;
        string IMainWindowActions.DbFullPath => MainVM.DbInfo.DBFullPath;
        ListView IMainWindowListViews.SmallList => SmallList;
        ListView IMainWindowListViews.BigList => BigList;
        ListView IMainWindowListViews.GridList => GridList;
        DataGrid IMainWindowListViews.ListDataGrid => ListDataGrid;
        ListView IMainWindowListViews.BigList10 => BigList10;

        void IMainWindowActions.RefreshExtDetail() => viewExtDetail.Refresh();

        void IMainWindowActions.RefreshActiveList(int tabIndex) =>
            TabListRefreshHelper.RefreshListByTabIndex(tabIndex, this);

        void IMainWindowActions.UpdateMovieColumn(long movieId, MovieColumn column, object value) =>
            UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, movieId, column, value);

        TabControl IMainWindowTabViews.Tabs => Tabs;
        TabItem IMainWindowTabViews.TabSmall => TabSmall;
        TabItem IMainWindowTabViews.TabBig => TabBig;
        TabItem IMainWindowTabViews.TabGrid => TabGrid;
        TabItem IMainWindowTabViews.TabList => TabList;
        TabItem IMainWindowTabViews.TabBig10 => TabBig10;
        UserControls.ExtDetail IMainWindowTabViews.ViewExtDetail => viewExtDetail;
    }
}