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
using System.Windows.Media;
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
        private Task _processorTask;
        private readonly CancellationTokenSource _processorCts = new();
        private readonly ThumbnailWorkScope _thumbnailWorkScope = new();
        private readonly ThumbnailQueueProcessor _thumbnailQueueProcessor = new();
        private readonly ThumbnailQueueScheduler _thumbnailScheduler = new();
        private readonly FileWatcherManager _fileWatcherManager = new();
        private readonly SemaphoreSlim _folderCheckGate = new(1, 1);
        private bool _openingDatabase;

        private Stack<string> recentFiles = new();

        private IEnumerable<MovieRecords> filterList = [];

        private string _cachedAllItemsSortId;
        private List<MovieRecords> _cachedAllItems;
        private int _cachedAllItemsSourceCount;

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

        // MediaElement の再生状態と UI を同期する
        private readonly DispatcherTimer timer;
        private readonly ManualThumbnailPreviewController _manualPreview;
        private bool isDragging = false;
        private bool _isPreviewMediaOpened;
        private bool _isUpdatingSliderFromPlayer;
        private double _pendingPreviewStartMs;
        private bool _applyPendingStartOnPlay;
        private bool _useLegacyPreviewFallback;

        //マニュアルサムネイル時の右クリックしたカラムの返却を受け取る変数
        private int manualPos = 0;

        private MovieRecords _contextMenuMovie;
        private System.Windows.Controls.Image _contextMenuThumbImage;
        private System.Windows.Point _contextMenuThumbClick;
        private bool _contextMenuThumbClickValid;
        private System.Windows.Controls.Image _lastClickedThumbImage;
        private System.Windows.Point _lastThumbClickOnImage;
        private bool _lastThumbClickValid;

        //IME起動中的なフラグ。日本語入力中（未変換）にインクリメンタルサーチさせない為。
        private bool _imeFlag = false;

        private readonly ThumbnailLayoutCache _thumbLayoutCache = new();
        private readonly MainWindowSessionState _sessionState = new();

        //private bool _searchBoxItemSelectedByMouse = false;
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

            if (Properties.Settings.Default.RecentFiles != null)
            {
                recentFiles = RecentFilesService.LoadFromSettings(Properties.Settings.Default.RecentFiles);
                RecentFilesService.RebuildRecentItems(MainVM.RecentFileItems, recentFiles);
            }

            DataContext = MainVM;

            if (File.Exists(ApplicationPaths.LayoutFilePath))
            {
                XmlLayoutSerializer layoutSerializer = new(uxDockingManager);
                using var reader = new StreamReader(ApplicationPaths.LayoutFilePath);
                layoutSerializer.Deserialize(reader);
            }

            #region Player Initialize
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            timer.Tick += new EventHandler(Timer_Tick);

            _manualPreview = new ManualThumbnailPreviewController(Dispatcher)
            {
                OnFrameReady = source => uxPreviewFallbackImage.Source = source
            };

            uxTime.Text = "00:00:00";
            uxVolume.Text = ((int)(uxVolumeSlider.Value * 100)).ToString();
            PlayerArea.Visibility = Visibility.Collapsed;
            PlayerController.Visibility = Visibility.Collapsed;
            uxPreviewImage.Visibility = Visibility.Collapsed;
            uxPreviewFallbackImage.Visibility = Visibility.Collapsed;
            #endregion
        }

        private void MainWindow_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                _ = InitializeAfterRenderAsync();

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

                // サムネイル監視タスクは temp 掃除後に起動（InitializeAfterRenderAsync）
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task InitializeAfterRenderAsync()
        {
            await Task.Run(ClearTempJpg).ConfigureAwait(true);

            if (_processorTask == null || _processorTask.IsCompleted)
            {
                _processorTask = CheckThumbAsync(_processorCts.Token);
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
                using var writer = new StreamWriter(ApplicationPaths.LayoutFilePath);
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
                _processorCts.Cancel();
            }
        }

        private void ClearThumbnailQueue() => _thumbnailScheduler.ClearQueue();

        private void CancelActiveThumbnailWork(int primaryTabIndex = 0)
        {
            ThumbnailQueueProcessor.RequestDismissProgress();
            _thumbnailWorkScope.CancelBatch();
            _thumbnailScheduler.AbandonAndClearQueue(primaryTabIndex);
            _sessionState.BumpThumbnailWorkGeneration();
        }

        private void AbandonThumbnailWorkForDbSwitch(int primaryTabIndex = 0) =>
            CancelActiveThumbnailWork(primaryTabIndex);

        private void StampQueueDbContext(QueueObj item)
        {
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.DbFullPath))
            {
                item.DbFullPath = MainVM.DbInfo.DBFullPath;
            }

            if (!string.IsNullOrWhiteSpace(item.MovieFullPath))
            {
                item.MovieFullPath = MediaPathNormalizer.Normalize(item.MovieFullPath);
            }

            item.WorkGeneration = _sessionState.ThumbnailWorkGeneration;
        }

        private void StampQueueDbContext(IEnumerable<QueueObj> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (QueueObj item in items)
            {
                StampQueueDbContext(item);
            }
        }

        private void EnqueueThumbnailWork(IReadOnlyList<QueueObj> items, int primaryTabIndex, bool beginNewJob = false)
        {
            StampQueueDbContext(items);
            _thumbnailScheduler.EnqueueWork(items, primaryTabIndex, beginNewJob);
        }

        private void EnqueueThumbnailWork(QueueObj item, int primaryTabIndex, bool beginNewJob = false)
        {
            StampQueueDbContext(item);
            _thumbnailScheduler.EnqueueWork(item, primaryTabIndex, beginNewJob);
        }

        private void EnqueueDiscoveredFileThumbnails(MovieInfo mvi, int primaryTabIndex, string dbFullPath)
        {
            CancelThumbnailWorkForMovie(mvi.MovieId);
            EnqueueThumbnailWork(
                [
                    new QueueObj
                    {
                        MovieId = mvi.MovieId,
                        MovieFullPath = mvi.MoviePath,
                        Tabindex = primaryTabIndex,
                        DbFullPath = dbFullPath,
                    },
                    new QueueObj
                    {
                        MovieId = mvi.MovieId,
                        MovieFullPath = mvi.MoviePath,
                        Tabindex = 99,
                        DbFullPath = dbFullPath,
                    },
                ],
                primaryTabIndex,
                beginNewJob: true);
        }

        private void EnsureDetailThumbnail(MovieRecords mv)
        {
            if (mv == null || string.IsNullOrWhiteSpace(mv.Movie_Path))
            {
                return;
            }

            string hash = GetHashCRC32(mv.Movie_Path);
            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            string movieBody = Path.GetFileNameWithoutExtension(mv.Movie_Name ?? mv.Movie_Path).ToLowerInvariant();
            string thumbFile = ThumbnailLayoutCache.GetThumbFileName(movieBody, hash);
            string expectedDetailPath = _thumbLayoutCache.GetExpectedThumbPath(99, movieBody, hash);

            mv.ThumbDetail = _thumbLayoutCache.BuildThumbPath(99, thumbFile, checkExists: true);

            bool detailMissing = !File.Exists(expectedDetailPath);

            if (ZipMediaKind.IsZipRecord(mv) || ZipMediaKind.IsZipPath(mv.Movie_Path))
            {
                if (ZipDetailThumbnailMaterializer.TryCopyFromExistingTabThumbs(
                        _thumbLayoutCache,
                        movieBody,
                        hash,
                        expectedDetailPath))
                {
                    mv.ThumbDetail = expectedDetailPath;
                    return;
                }
            }

            if (!detailMissing || _thumbnailScheduler.JobCoordinator.IsInFlight(mv.Movie_Id, 99))
            {
                return;
            }

            var item = new QueueObj
            {
                MovieId = mv.Movie_Id,
                MovieFullPath = mv.Movie_Path,
                Tabindex = 99,
                DbFullPath = MainVM.DbInfo.DBFullPath,
            };
            StampQueueDbContext(item);
            _thumbnailScheduler.TryEnqueueDetailWork(item);
        }

        private void EnqueueSilentThumbnailWork(QueueObj item)
        {
            StampQueueDbContext(item);
            _thumbnailScheduler.EnqueueSilentWork(item);
        }

        private bool TryEnqueueManualThumbnailWork(QueueObj item)
        {
            StampQueueDbContext(item);
            return _thumbnailScheduler.TryEnqueueManualWork(item);
        }

        private void CancelThumbnailWorkForMovie(long movieId) =>
            _thumbnailScheduler.CancelTrackedForMovie(movieId);

        private void StartTabSwitchThumbnailJob(int tabIndex) =>
            _thumbnailScheduler.StartTabSwitchJob(
                tabIndex,
                filterList,
                _thumbLayoutCache,
                MainVM.DbInfo.DBFullPath,
                _sessionState.ThumbnailWorkGeneration);

        private static int GetThumbnailQueueMaxParallelism() => ThumbnailQueueScheduler.GetMaxParallelism();

        /// <summary>
        /// ファイル追加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileChanged(FileSystemEventArgs e, int watcherSession) =>
            _ = HandleFileChangedAsync(e, watcherSession);

        private async Task HandleFileChangedAsync(FileSystemEventArgs e, int watcherSession)
        {
            if (!_fileWatcherManager.IsSessionActive(watcherSession))
            {
                return;
            }

            try
            {
                (bool shouldProcess, string dbPath) = await Dispatcher.InvokeAsync(() =>
                {
                    if (!_fileWatcherManager.IsSessionActive(watcherSession))
                    {
                        return (false, "");
                    }

                    if (e.ChangeType != WatcherChangeTypes.Created
                        && e.ChangeType != WatcherChangeTypes.Changed)
                    {
                        return (false, "");
                    }

                    if (!MediaExtensionSettings.ShouldScanFile(
                            e.FullPath,
                            Properties.Settings.Default.CheckExt,
                            MainVM.DbInfo.ExcludeExt))
                    {
                        return (false, "");
                    }

                    string path = MainVM.DbInfo.DBFullPath;
                    return (string.IsNullOrWhiteSpace(path) ? false : true, path);
                }).Task.ConfigureAwait(false);

                if (!shouldProcess || string.IsNullOrWhiteSpace(dbPath))
                {
                    return;
                }

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
                        await Task.Delay(1000).ConfigureAwait(false);
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

                if (!_fileWatcherManager.IsSessionActive(watcherSession))
                {
                    return;
                }

                bool alreadyRegistered = await Dispatcher.InvokeAsync(() =>
                {
                    if (!_fileWatcherManager.IsSessionActive(watcherSession))
                    {
                        return true;
                    }

                    return !FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, e.FullPath);
                }).Task.ConfigureAwait(false);

                if (alreadyRegistered)
                {
                    return;
                }

                MovieInfo mvi = await MovieRegistrationHelper
                    .TryRegisterDiscoveredFileAsync(dbPath, e.FullPath)
                    .ConfigureAwait(false);
                if (mvi == null)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(async () =>
                {
                    if (!_fileWatcherManager.IsSessionActive(watcherSession))
                    {
                        return;
                    }

                    string sortId = MainVM.DbInfo.Sort ?? "1";
                    await FilterAndSortAsync(sortId, true).ConfigureAwait(true);

                    if (!_fileWatcherManager.IsSessionActive(watcherSession))
                    {
                        return;
                    }

                    int tabIndex = MainVM.DbInfo.CurrentTabIndex;
                    if (tabIndex < 0)
                    {
                        return;
                    }

                    EnqueueDiscoveredFileThumbnails(mvi, tabIndex, dbPath);
                }).Task.Unwrap().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"FileChangedで例外発生: {ex.Message}");
#endif
                await UiDispatcherHelper.RunOnUiAsync(
                    Dispatcher,
                    () => MessageBox.Show(
                        this,
                        $"ファイル変更の処理中にエラーが発生しました。\n{ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
            }
        }

        /// <summary>
        /// ファイル名変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileRenamed(RenamedEventArgs e, int watcherSession)
        {
            if (!_fileWatcherManager.IsSessionActive(watcherSession))
            {
                return;
            }

            var eFullPath = e.FullPath;
            var oldFullPath = e.OldFullPath;

            _ = Dispatcher.InvokeAsync(() =>
            {
                if (!_fileWatcherManager.IsSessionActive(watcherSession))
                {
                    return;
                }

                if (!MediaExtensionSettings.ShouldScanFile(
                        eFullPath,
                        Properties.Settings.Default.CheckExt,
                        MainVM.DbInfo.ExcludeExt))
                {
                    return;
                }

#if DEBUG
                string s = string.Format($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} :");
                s += $"【{e.ChangeType}】{e.OldName} → {eFullPath}";
                Debug.WriteLine(s);
#endif
                RenameThumb(eFullPath, oldFullPath);
            });
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
            _openingDatabase = true;
            try
            {
                //強制的に-1にする。前回のタブが0だった場合の対応
                Tabs.SelectedIndex = -1;
                CancelActiveThumbnailWork();
                _fileWatcherManager.Clear();
                watchData?.Clear();
                MainVM.DbInfo.SearchKeyword = "";
                _movieRecordsLoaded = false;
                InvalidateAllItemsFilterCache();
                _sessionState.SetActiveDb(dbFullPath);

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
            finally
            {
                _openingDatabase = false;
            }
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
                _sessionState.BumpFilterGeneration();
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
                StoreAllItemsFilterCache(sortId, loaded.Records);
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
            _ = RunStartupFolderCheckAsync();
        }

        private async Task RunStartupFolderCheckAsync()
        {
            await Task.Yield();
            try
            {
                await CheckFolderAsync(FolderCheckMode.Auto).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [folder-check] {ex.Message}");
            }
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

        private void InvalidateAllItemsFilterCache()
        {
            _cachedAllItemsSortId = null;
            _cachedAllItems = null;
            _cachedAllItemsSourceCount = 0;
        }

        private void StoreAllItemsFilterCache(string sortId, IReadOnlyList<MovieRecords> items)
        {
            _cachedAllItemsSortId = sortId;
            _cachedAllItems = items as List<MovieRecords> ?? [.. items];
            _cachedAllItemsSourceCount = MainVM.MovieRecs.Count;
        }

        private bool TryGetCachedAllItemsFilter(string sortId, out MovieListCoordinator.FilterApplyResult result)
        {
            if (_cachedAllItems != null
                && _cachedAllItemsSortId == sortId
                && _cachedAllItemsSourceCount == MainVM.MovieRecs.Count)
            {
                result = new MovieListCoordinator.FilterApplyResult
                {
                    Items = _cachedAllItems,
                    SearchCount = _cachedAllItems.Count
                };
                return true;
            }

            result = null;
            return false;
        }

        private void SetActiveTabItemsSource(IEnumerable<MovieRecords> items)
        {
            switch (Tabs?.SelectedIndex ?? 0)
            {
                case 0:
                    SmallList.ItemsSource = items;
                    break;
                case 1:
                    BigList.ItemsSource = items;
                    break;
                case 2:
                    GridList.ItemsSource = items;
                    break;
                case 3:
                    ListDataGrid.ItemsSource = items;
                    break;
                case 4:
                    BigList10.ItemsSource = items;
                    break;
                default:
                    SmallList.ItemsSource = items;
                    break;
            }
        }

        private void SetAllTabItemsSource(IEnumerable<MovieRecords> items)
        {
            SmallList.ItemsSource = items;
            BigList.ItemsSource = items;
            GridList.ItemsSource = items;
            ListDataGrid.ItemsSource = items;
            BigList10.ItemsSource = items;
        }

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
            bool cacheHit = false;
#endif
            int capturedGeneration = _sessionState.FilterGeneration;
            string searchKeyword = MainVM.DbInfo.SearchKeyword ?? "";
            bool showAll = string.IsNullOrEmpty(searchKeyword);

            MovieListCoordinator.FilterApplyResult result;
            if (showAll && TryGetCachedAllItemsFilter(id, out result))
            {
#if DEBUG
                cacheHit = true;
#endif
            }
            else
            {
                List<MovieRecords> snapshot = [.. MainVM.MovieRecs];
                int currentTabIndex = Tabs?.SelectedIndex ?? MainVM.DbInfo.CurrentTabIndex;
                var filterContext = new MovieListFilterContext
                {
                    CurrentTabIndex = currentTabIndex,
                    ThumbnailCache = _thumbLayoutCache,
                };
                result = await Task.Run(() =>
                    MovieListCoordinator.ApplyFilter(snapshot, searchKeyword, id, filterContext)).ConfigureAwait(true);

                if (showAll)
                {
                    StoreAllItemsFilterCache(id, result.Items);
                }
            }

            if (capturedGeneration != _sessionState.FilterGeneration)
            {
                return;
            }

            filterList = result.Items;
            MainVM.DbInfo.SearchCount = result.SearchCount;

            viewExtDetail.Visibility = MainVM.DbInfo.SearchCount == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (showAll)
            {
                SetActiveTabItemsSource(filterList);
            }
            else
            {
                SetAllTabItemsSource(filterList);
                Refresh();
            }
#if DEBUG
            sw.Stop();
            Debug.WriteLine($"絞り込み経過時間 FilterAndSort：{sw.ElapsedMilliseconds} ミリ秒 (showAll={showAll}, cacheHit={cacheHit})");
#endif
        }

        private void DataRowToViewData(DataRow row, int? resolveTabIndexOnly = null)
        {
            int tabCount = Tabs?.Items?.Count ?? _thumbLayoutCache.TabOutPaths.Length;
            MainVM.MovieRecs.Add(
                MovieRecordMapper.FromDataRow(row, _thumbLayoutCache, tabCount, resolveTabIndexOnly)
            );
        }

        private void ResolveThumbPathsForTab(int tabIndex, IEnumerable<MovieRecords> records = null) =>
            ThumbPathHelper.ResolveThumbPathsForTab(records ?? filterList, _thumbLayoutCache, tabIndex);

        private void Tabs_SelectionChangedAsync(object sender, SelectionChangedEventArgs e)
        {
            if (sender as TabControl != null && e.OriginalSource is TabControl)
            {
                var tabControl = sender as TabControl;
                int index = tabControl.SelectedIndex;
                if (index == -1) return;

                MainVM.DbInfo.CurrentTabIndex = index;

                if (!filterList.Any() || _openingDatabase)
                {
                    return;
                }

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
                CancelThumbnailWorkForMovie(rec.Movie_Id);

                if (dialogWindow.checkBox.IsChecked == true)
                {
                    ThumbnailDeletionHelper.DeleteThumbnailsForRecord(
                        MainVM.DbInfo.ThumbFolder,
                        MainVM.DbInfo.DBName,
                        rec.Movie_Body,
                        rec.Hash);
                }
                DeleteMovieTable(MainVM.DbInfo.DBFullPath, rec.Movie_Id);

                MovieRecords stale = MainVM.MovieRecs.FirstOrDefault(x => x.Movie_Id == rec.Movie_Id);
                if (stale != null)
                {
                    MainVM.MovieRecs.Remove(stale);
                }

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
            if (!SinkuMetadataFetcher.IsAvailable
                || string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
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

            if (!SinkuMetadataFetcher.IsAvailable
                || string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
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
            RecentFilesService.RebuildRecentItems(MainVM.RecentFileItems, recentFiles);
        }

        private void PersistRecentFilesToSettings()
        {
            Properties.Settings.Default.RecentFiles.Clear();
            Properties.Settings.Default.RecentFiles.AddRange([.. recentFiles.Reverse()]);
            Properties.Settings.Default.Save();
        }

        private NavigationDrawerItem _recentFileRemoveTarget;

        private void RecentNavList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            NavigationDrawerItem item = TryGetNavigationItem(e);
            if (item == null || string.IsNullOrEmpty(item.Id))
            {
                return;
            }

            _recentFileRemoveTarget = item;
            RecentFileRemovePopup.IsOpen = true;
            e.Handled = true;
        }

        private void RemoveRecentFile_Click(object sender, RoutedEventArgs e)
        {
            string path = _recentFileRemoveTarget?.Id ?? "";
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            recentFiles = RecentFilesService.Remove(recentFiles, path);
            RecentFilesService.RebuildRecentItems(MainVM.RecentFileItems, recentFiles);
            PersistRecentFilesToSettings();
            RecentFileRemovePopup.IsOpen = false;
            _recentFileRemoveTarget = null;
        }

        private void NavigationList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || sender is not ListBox listBox)
            {
                return;
            }

            NavigationDrawerItem item = TryGetNavigationItem(e);
            if (item == null || string.IsNullOrEmpty(item.Id) || !item.IsEnabled)
            {
                return;
            }

            switch (listBox.Name)
            {
                case nameof(PrimaryNavList):
                    ExecutePrimaryNavigation(item.Id);
                    break;
                case nameof(RecentNavList):
                    OpenRecentFile(item.Id);
                    break;
                case nameof(SettingsNavList):
                    ExecuteSettingsNavigation(item.Id);
                    break;
                case nameof(ToolNavList):
                    ExecuteToolNavigation(item.Id);
                    break;
                case nameof(ExitNavList):
                    BtnExit_Click(sender, e);
                    break;
            }

            listBox.SelectedIndex = -1;
            e.Handled = true;
        }

        private static NavigationDrawerItem TryGetNavigationItem(MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject current)
            {
                return null;
            }

            while (current != null)
            {
                if (current is ListBoxItem { DataContext: NavigationDrawerItem item })
                {
                    return item;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void ExecutePrimaryNavigation(string actionId)
        {
            switch (actionId)
            {
                case NavigationActionIds.New:
                    BtnNew_Click(this, new RoutedEventArgs());
                    break;
                case NavigationActionIds.Open:
                    BtnOpen_Click(this, new RoutedEventArgs());
                    break;
            }
        }

        private void OpenRecentFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            MenuToggleButton.IsChecked = false;
            if (!string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                UpdateSkin();
                UpdateSort();
            }

            ReStackRecentTree(path);
            OpenDatafile(path);
            Properties.Settings.Default.LastDoc = path;
            Properties.Settings.Default.Save();
        }

        private void ExecuteSettingsNavigation(string menuId)
        {
            switch (menuId)
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
                    UpsertSystemTable(
                        MainVM.DbInfo.DBFullPath,
                        "excludeExt",
                        MediaExtensionSettings.NormalizeListForStorage(settingsWindow.ExcludeExt.Text));

                    GetSystemTable(MainVM.DbInfo.DBFullPath);
                    break;
            }
        }

        private void ExecuteToolNavigation(string menuId)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                MessageBox.Show("管理ファイルが選択されていません。", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            MenuToggleButton.IsChecked = false;

            switch (menuId)
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
            }
        }

        private static string GetLastOpenInitialDirectory()
        {
            string lastDoc = Properties.Settings.Default.LastDoc;
            if (!string.IsNullOrWhiteSpace(lastDoc))
            {
                string directory = Path.GetDirectoryName(lastDoc);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = GetLastOpenInitialDirectory(),
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
                    string bookMarkedFilePath = BookmarkSourceResolver.ResolveSourceMoviePath(mv, MainVM.MovieRecs);
                    if (string.IsNullOrWhiteSpace(bookMarkedFilePath) || !Path.Exists(bookMarkedFilePath))
                    {
                        return;
                    }

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
                    if (TryResolvePlayPositionFromThumb(mv, Tabs.SelectedIndex, out int panelIndex, out msec))
                    {
                        secPos = panelIndex;
                    }
                    else
                    {
                        msec = GetPlayPosition(Tabs.SelectedIndex, mv, ref secPos);
                    }
                }

                if (ZipMediaKind.IsZipRecord(mv))
                {
                    ZipImageViewerLauncher.TryOpen(
                        mv.Movie_Path,
                        SelectSystemTable("playerPrg"),
                        SelectSystemTable("playerParam"),
                        Properties.Settings.Default.DefaultZipViewerPath,
                        Properties.Settings.Default.DefaultZipViewerParam);
                    ExternalPlayerLauncher.ApplyPlaybackStats(mv, MainVM.DbInfo.DBFullPath);
                    return;
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

            string text = SearchBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                MainVM.DbInfo.SearchKeyword = "";
                await FilterAndSortAsync(MainVM.DbInfo.Sort, false).ConfigureAwait(true);
                SelectFirstItem();
            }
        }

        private void SearchBox_DropDownClosed(object sender, EventArgs e)
        {
        }

        // ドロップダウンリスト内でマウスクリック時に検索
        private async void SearchBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ComboBoxItem item)
            {
                return;
            }

            e.Handled = true;
            string keyword = item.Content?.ToString() ?? "";
            if (item.DataContext is History history)
            {
                keyword = history.Find_Text ?? "";
            }

            SearchBox.IsDropDownOpen = false;
            await SearchByKeywordAsync(keyword).ConfigureAwait(true);
        }

        private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_imeFlag) { return; }
            if (sender is not ComboBox combo)
            {
                return;
            }

            if (combo.IsDropDownOpen
                && e.Key == Key.Enter
                && combo.SelectedItem is History enterHistory)
            {
                e.Handled = true;
                combo.IsDropDownOpen = false;
                await SearchByKeywordAsync(enterHistory.Find_Text ?? "").ConfigureAwait(true);
                return;
            }

            // Deleteキーで履歴削除
            if (e.Key == Key.Delete && combo.IsDropDownOpen && combo.SelectedItem is History deleteHistory)
            {
                e.Handled = true;

                string keepText = combo.Text;
                long findId = deleteHistory.Find_Id;

                _isDeletingSearchHistory = true;
                try
                {
                    MainVM.HistoryRecs.Remove(deleteHistory);
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
                await SearchByKeywordAsync(combo.Text ?? "").ConfigureAwait(true);
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
            EnsureDetailThumbnail(mv);
        }

        private void ListDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            e.Cancel = true;
        }

        private int GetPlayPosition(int tabIndex, MovieRecords mv, ref int returnPos)
        {
            if (_lastThumbClickValid
                && _lastClickedThumbImage != null
                && _lastClickedThumbImage.ActualWidth > 0
                && _lastClickedThumbImage.ActualHeight > 0)
            {
                return PlayPositionResolver.GetPlayPositionMsec(
                    _lastThumbClickOnImage,
                    _lastClickedThumbImage.ActualWidth,
                    _lastClickedThumbImage.ActualHeight,
                    tabIndex,
                    mv,
                    ref returnPos);
            }

            return 0;
        }

        private bool TryResolvePlayPositionFromThumb(MovieRecords mv, int tabIndex, out int panelIndex, out int positionMsec)
        {
            panelIndex = 0;
            positionMsec = 0;

            if (_contextMenuThumbClickValid
                && _contextMenuThumbImage != null
                && _contextMenuThumbImage.ActualWidth > 0
                && _contextMenuThumbImage.ActualHeight > 0
                && ThumbPanelHitResolver.TryResolveFromImageClick(
                    _contextMenuThumbClick,
                    _contextMenuThumbImage.ActualWidth,
                    _contextMenuThumbImage.ActualHeight,
                    PlayPositionResolver.GetThumbPathForTab(mv, tabIndex),
                    ZipMediaKind.IsZipRecord(mv),
                    out panelIndex,
                    out positionMsec))
            {
                return true;
            }

            if (_lastThumbClickValid
                && _lastClickedThumbImage != null
                && _lastClickedThumbImage.ActualWidth > 0
                && _lastClickedThumbImage.ActualHeight > 0
                && ThumbPanelHitResolver.TryResolveFromImageClick(
                    _lastThumbClickOnImage,
                    _lastClickedThumbImage.ActualWidth,
                    _lastClickedThumbImage.ActualHeight,
                    PlayPositionResolver.GetThumbPathForTab(mv, tabIndex),
                    ZipMediaKind.IsZipRecord(mv),
                    out panelIndex,
                    out positionMsec))
            {
                return true;
            }

            return false;
        }

        private void MenuContext_Opened(object sender, RoutedEventArgs e)
        {
            _contextMenuMovie = null;

            if (sender is not ContextMenu menu)
            {
                return;
            }

            if (menu.PlacementTarget is FrameworkElement target)
            {
                _contextMenuMovie = ResolveMovieRecordsFromElement(target);
                if (!_contextMenuThumbClickValid)
                {
                    _contextMenuThumbImage = FindDescendant<System.Windows.Controls.Image>(target);
                    if (_contextMenuThumbImage != null)
                    {
                        _contextMenuThumbClick = Mouse.GetPosition(_contextMenuThumbImage);
                        _contextMenuThumbClickValid = true;
                    }
                }

                if (_contextMenuMovie != null)
                {
                    SelectMovieRecord(_contextMenuMovie);
                }
            }

            bool isZip = ZipMediaKind.IsZipRecord(_contextMenuMovie);
            bool sinkuAvailable = SinkuMetadataFetcher.IsAvailable;
            foreach (object item in menu.Items)
            {
                if (item is not MenuItem menuItem)
                {
                    continue;
                }

                if (menuItem.Name == "ManualThumbnail" || menuItem.Name == "PlayFromThumb")
                {
                    menuItem.IsEnabled = !isZip;
                }
                else if (menuItem.Name == "RefreshFileInfo")
                {
                    menuItem.IsEnabled = sinkuAvailable;
                }
            }
        }

        private void ThumbnailImage_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Image image)
            {
                return;
            }

            _contextMenuThumbImage = image;
            _contextMenuThumbClick = e.GetPosition(image);
            _contextMenuThumbClickValid = true;

            if (FindAncestor<ListViewItem>(image) is ListViewItem item
                && item.DataContext is MovieRecords record)
            {
                _contextMenuMovie = record;
                SelectMovieRecord(record);
            }
        }

        private static MovieRecords ResolveMovieRecordsFromElement(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is MovieRecords record)
                {
                    return record;
                }

                current = current is FrameworkElement parentFe
                    ? parentFe.Parent
                    : LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private void SelectMovieRecord(MovieRecords record)
        {
            if (record == null)
            {
                return;
            }

            switch (Tabs.SelectedIndex)
            {
                case 0:
                    SmallList.SelectedItem = record;
                    break;
                case 1:
                    BigList.SelectedItem = record;
                    break;
                case 2:
                    GridList.SelectedItem = record;
                    break;
                case 3:
                    ListDataGrid.SelectedItem = record;
                    break;
                case 4:
                    BigList10.SelectedItem = record;
                    break;
            }
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    return match;
                }

                T nested = FindDescendant<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

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
                _lastClickedThumbImage = FindDescendant<System.Windows.Controls.Image>(label);
                if (_lastClickedThumbImage != null)
                {
                    _lastThumbClickOnImage = e.GetPosition(_lastClickedThumbImage);
                    _lastThumbClickValid = true;
                }
                else
                {
                    _lastThumbClickValid = false;
                }
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
            await _folderCheckGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await CheckFolderCoreAsync(mode).ConfigureAwait(false);
            }
            finally
            {
                _folderCheckGate.Release();
            }
        }

        private async Task CheckFolderCoreAsync(FolderCheckMode mode)
        {
            (int folderCheckGeneration, string dbFullPath, string excludeExt, List<(string Folder, bool Sub)> foldersToCheck) =
                await Dispatcher.InvokeAsync(() =>
                {
                    int generation = _sessionState.FolderCheckGeneration;
                    string dbPath = MainVM.DbInfo.DBFullPath;
                    if (string.IsNullOrWhiteSpace(dbPath))
                    {
                        return (generation, dbPath, "", new List<(string Folder, bool Sub)>());
                    }

                    MediaExtensionSettings.EnsureRequiredExtensions();
                    GetWatchTable(dbPath, FolderCheckService.GetWatchSql(mode));
                    return (generation, dbPath, MainVM.DbInfo.ExcludeExt ?? "", FolderCheckService.GetFoldersToCheck(watchData));
                }).Task.ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(dbFullPath) || foldersToCheck.Count == 0)
            {
                return;
            }

            bool folderCheckStillActive() =>
                folderCheckGeneration == _sessionState.FolderCheckGeneration
                && _sessionState.IsActiveDb(dbFullPath);

            if (!folderCheckStillActive())
            {
                return;
            }

            bool FolderCheckflg = false;
            bool foundUnregistered = false;
            List<QueueObj> addFiles = [];
            int totalFolders = foldersToCheck.Count;
            FolderCheckProgressSession folderCheckProgress =
                await BeginFolderCheckProgressAsync(totalFolders).ConfigureAwait(true);

            MoviePathRegistrationIndex pathIndex = await Task.Run(() =>
                MoviePathRegistrationIndex.Load(dbFullPath)).ConfigureAwait(false);

            try
            {
                for (int folderIndex = 0; folderIndex < foldersToCheck.Count; folderIndex++)
                {
                    if (!folderCheckStillActive())
                    {
                        return;
                    }

                    (string checkFolder, bool sub) = foldersToCheck[folderIndex];
                    await ReportFolderCheckProgressAsync(
                        folderCheckProgress,
                        folderIndex,
                        $"{checkFolder} 監視実施中…").ConfigureAwait(true);

                    List<string> unregisteredFiles;
                    try
                    {
                        unregisteredFiles = await Task.Run(() =>
                            MoviePathRegistrationIndex.FindUnregisteredFiles(pathIndex, checkFolder, sub, excludeExt))
                            .ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (e is IOException)
                        {
                            await Task.Delay(1000).ConfigureAwait(false);
                        }

                        unregisteredFiles = [];
                    }

                    if (!folderCheckStillActive())
                    {
                        return;
                    }

                    if (unregisteredFiles.Count > 0)
                    {
                        foundUnregistered = true;
                        await ReportFolderCheckProgressAsync(
                            folderCheckProgress,
                            folderIndex,
                            $"{checkFolder} に更新あり。").ConfigureAwait(true);
                    }

                    foreach (string fileFullPath in unregisteredFiles)
                    {
                        if (!folderCheckStillActive())
                        {
                            return;
                        }

                        try
                        {
                            MovieInfo mvi = await MovieRegistrationHelper
                                .TryRegisterDiscoveredFileAsync(dbFullPath, fileFullPath)
                                .ConfigureAwait(false);
                            if (mvi == null)
                            {
                                continue;
                            }

                            pathIndex.Register(mvi.MoviePath);
                            FolderCheckflg = true;

                            int tabIndex = await Dispatcher.InvokeAsync(() => MainVM.DbInfo.CurrentTabIndex);

                            CancelThumbnailWorkForMovie(mvi.MovieId);
                            addFiles.Add(new QueueObj
                            {
                                MovieId = mvi.MovieId,
                                MovieFullPath = mvi.MoviePath,
                                Tabindex = tabIndex,
                                DbFullPath = dbFullPath,
                            });
                            addFiles.Add(new QueueObj
                            {
                                MovieId = mvi.MovieId,
                                MovieFullPath = mvi.MoviePath,
                                Tabindex = 99,
                                DbFullPath = dbFullPath,
                            });
                        }
                        catch (Exception)
                        {
#if DEBUG
                            Debug.WriteLine(
                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [folder-check] skip {fileFullPath}");
#endif
                        }
                    }

                    if (!folderCheckStillActive())
                    {
                        return;
                    }

                    await ReportFolderCheckProgressAsync(
                        folderCheckProgress,
                        folderIndex + 1,
                        $"{checkFolder} 監視完了").ConfigureAwait(true);
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
            finally
            {
                await EndFolderCheckProgressAsync(folderCheckProgress).ConfigureAwait(true);
            }

            if (!folderCheckStillActive() || (!FolderCheckflg && !foundUnregistered))
            {
                return;
            }

            await Dispatcher.InvokeAsync(async () =>
            {
                if (!folderCheckStillActive())
                {
                    return;
                }

                int primaryTabIndex = MainVM.DbInfo.CurrentTabIndex;
                string sortId = MainVM.DbInfo.Sort ?? "1";
                await FilterAndSortAsync(sortId, true).ConfigureAwait(true);

                if (!folderCheckStillActive())
                {
                    return;
                }

                if (FolderCheckflg && addFiles.Count > 0)
                {
                    EnqueueThumbnailWork(addFiles, primaryTabIndex, beginNewJob: true);
                }
            }).Task.Unwrap().ConfigureAwait(false);
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
                        (queueObj, token) => CreateThumbAsync(queueObj, queueObj.IsManual, token),
                        _thumbnailScheduler.JobCoordinator,
                        _thumbnailScheduler.SyncRoot,
                        maxParallelism: GetThumbnailQueueMaxParallelism(),
                        maxParallelismResolver: GetThumbnailQueueMaxParallelism,
                        pollIntervalMs: 100,
                        cts: cts,
                        batchCancellationToken: () => _thumbnailWorkScope.Token
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

        private ThumbnailCreationHost CreateThumbnailHost(QueueObj queueObj)
        {
            string dbFullPath = queueObj?.DbFullPath ?? "";
            int workGeneration = queueObj?.WorkGeneration ?? 0;
            string dbName = null;
            string thumbFolder = null;

            if (string.IsNullOrEmpty(dbFullPath))
            {
                return CreateInactiveThumbnailHost();
            }

            RunOnUi(() =>
            {
                if (!_sessionState.IsActiveDb(dbFullPath))
                {
                    return;
                }

                dbName = MainVM.DbInfo.DBName;
                thumbFolder = MainVM.DbInfo.ThumbFolder;
            });

            string capturedDbFullPath = dbFullPath;
            int capturedWorkGeneration = workGeneration;
            return new()
            {
                DbFullPath = capturedDbFullPath,
                DbName = dbName,
                ThumbFolder = thumbFolder,
                LayoutCache = _thumbLayoutCache,
                RunOnUi = RunOnUi,
                ApplyThumbPathsOnUi = ApplyThumbPathsOnUi,
                ApplyFailurePlaceholder = ApplyThumbnailFailurePlaceholder,
                IsResizeThumb = Properties.Settings.Default.IsResizeThumb,
                UpdateMovieColumn = (dbPath, movieId, value) =>
                    UpdateMovieSingleColumn(dbPath, movieId, "movie_length", value),
                IsSessionActive = () =>
                    _sessionState.IsActiveDb(capturedDbFullPath)
                    && capturedWorkGeneration == _sessionState.ThumbnailWorkGeneration,
                FindMovieRecord = _ => FindMovieRecordForQueue(queueObj),
            };
        }

        private static ThumbnailCreationHost CreateInactiveThumbnailHost() =>
            new()
            {
                DbFullPath = "",
                DbName = "",
                ThumbFolder = "",
                LayoutCache = null,
                RunOnUi = _ => { },
                ApplyThumbPathsOnUi = (_, _) => { },
                ApplyFailurePlaceholder = (_, _) => { },
                IsResizeThumb = false,
                UpdateMovieColumn = (_, _, _) => { },
                IsSessionActive = () => false,
                FindMovieRecord = _ => null,
            };

        private MovieRecords FindMovieRecordForQueue(QueueObj queueObj)
        {
            if (queueObj == null)
            {
                return null;
            }

            string normalizedQueuePath = MediaPathNormalizer.Normalize(queueObj.MovieFullPath);
            MovieRecords found = null;
            RunOnUi(() =>
            {
                found = MainVM.MovieRecs.FirstOrDefault(x => x.Movie_Id == queueObj.MovieId);
                if (found == null
                    && !string.IsNullOrWhiteSpace(normalizedQueuePath))
                {
                    found = MainVM.MovieRecs.FirstOrDefault(x =>
                        string.Equals(
                            MediaPathNormalizer.Normalize(x.Movie_Path),
                            normalizedQueuePath,
                            StringComparison.OrdinalIgnoreCase));
                }
            });
            return found;
        }

        private Task CreateThumbAsync(QueueObj queueObj, bool isManual = false, CancellationToken cts = default) =>
            ThumbnailCreationOrchestrator.CreateAsync(CreateThumbnailHost(queueObj), queueObj, isManual, cts);

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

        private void ApplyThumbPaths(QueueObj queueObj, string saveThumbFileName)
        {
            ThumbPathHelper.ApplyThumbPaths(MainVM.MovieRecs, queueObj, saveThumbFileName);

            if (queueObj.Tabindex is >= 0 and <= 4)
            {
                MovieRecords mv = MainVM.MovieRecs.FirstOrDefault(x => x.Movie_Id == queueObj.MovieId);
                if (mv != null)
                {
                    EnsureDetailThumbnail(mv);
                }
            }
        }

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
            SetPreviewModeUi(_useLegacyPreviewFallback);
            ApplyManualPreviewTimerInterval();

            if (_useLegacyPreviewFallback)
            {
                IsPlaying = true;
                timer.Start();
                return;
            }

            if (!_isPreviewMediaOpened)
            {
                return;
            }

            if (_applyPendingStartOnPlay)
            {
                SetPreviewPositionMs(_pendingPreviewStartMs);
                uxTime.Text = _manualPreview.PositionText;
                _applyPendingStartOnPlay = false;
            }

            IsPlaying = true;
            uxPreviewImage.Volume = uxVolumeSlider.Value;
            uxPreviewImage.Play();
            uxTimeSlider.Value = GetPreviewPositionMs();
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
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }
        }

        private void UxPreviewImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_manualPreview.IsOpen) { return; }

            if (IsPlaying)
            {
                Pause_Click(sender, e);
                return;
            }

            Start_Click(sender, e);
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
            if (_isUpdatingSliderFromPlayer) { return; }

            DateTime now = DateTime.Now;
            TimeSpan timeSinceLastUpdate = now - _lastSliderTime;

            if (timeSinceLastUpdate >= _timeSliderInterval)
            {
                SetPreviewPositionMs(uxTimeSlider.Value);
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

            if (_isPreviewMediaOpened)
            {
                uxPreviewImage.Volume = uxVolumeSlider.Value;
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
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }

            QueueObj queueObj = new()
            {
                MovieId = mv.Movie_Id,
                MovieFullPath = mv.Movie_Path,
                Tabindex = Tabs.SelectedIndex,
                ThumbPanelPos = manualPos,
                ThumbTimePos = _manualPreview.PositionSeconds,
                IsManual = true
            };

            CloseManualThumbnailPreview();

            await EnqueueManualThumbnailWorkAsync(queueObj);
        }

        private async Task EnqueueManualThumbnailWorkAsync(QueueObj queueObj)
        {
            for (int i = 0; i < 120; i++)
            {
                if (TryEnqueueManualThumbnailWork(queueObj))
                {
                    return;
                }

                await Task.Delay(500).ConfigureAwait(true);
            }

            MessageBox.Show(
                this,
                "サムネイル作成が混み合っています。しばらくしてから再度お試しください。",
                Assembly.GetExecutingAssembly().GetName().Name,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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

            MovieRecords mv = null;
            if (_manualPreview.IsOpen)
            {
                mv = BookmarkSourceResolver.FindMovieRecordByPath(MainVM.MovieRecs, _manualPreview.MoviePath);
            }

            mv ??= GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            timer.Stop();
            IsPlaying = false;
            if (_isPreviewMediaOpened && !_useLegacyPreviewFallback)
            {
                uxPreviewImage.Pause();
            }

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
            InsertBookmarkTable(MainVM.DbInfo.DBFullPath, mvi, mv.Movie_Path, mv.Hash);
            GetBookmarkTable();
            BookmarkList.Items.Refresh();
        }

        private async void ManualThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == null) { return; }

            MovieRecords mv = _contextMenuMovie ?? GetSelectedItemByTabIndex();
            if (mv == null) { return; }

            if (ZipMediaKind.IsZipRecord(mv))
            {
                return;
            }

            int msec = 0;
            if (sender is MenuItem senderObj && senderObj.Name == "ManualThumbnail")
            {
                if (_contextMenuThumbClickValid
                    && _contextMenuThumbImage != null
                    && ThumbPanelHitResolver.TryResolveFromImageClick(
                        _contextMenuThumbClick,
                        _contextMenuThumbImage.ActualWidth,
                        _contextMenuThumbImage.ActualHeight,
                        PlayPositionResolver.GetThumbPathForTab(mv, Tabs.SelectedIndex),
                        ZipMediaKind.IsZipRecord(mv),
                        out int panelIndex,
                        out msec))
                {
                    manualPos = panelIndex;
                }
                else
                {
                    msec = GetPlayPosition(Tabs.SelectedIndex, mv, ref manualPos);
                }
            }

            await _manualPreview.OpenAsync(mv.Movie_Path, msec);
            _pendingPreviewStartMs = _manualPreview.PositionMs;
            _applyPendingStartOnPlay = true;
            _useLegacyPreviewFallback = false;
            _isPreviewMediaOpened = false;
            uxPreviewFallbackImage.Source = null;
            uxPreviewImage.Stop();
            uxPreviewImage.Source = new Uri(mv.Movie_Path, UriKind.Absolute);
            uxPreviewImage.Volume = uxVolumeSlider.Value;

            uxTimeSlider.Maximum = Math.Max(_manualPreview.DurationMs, _manualPreview.PositionMs);
            uxTimeSlider.Value = _manualPreview.PositionMs;
            uxTime.Text = _manualPreview.PositionText;
            IsPlaying = false;
            PlayerArea.Visibility = Visibility.Visible;
            PlayerController.Visibility = Visibility.Visible;
            SetPreviewModeUi(useLegacyFallback: false);
            uxTimeSlider.Focus();
        }

        private void CloseManualThumbnailPreview()
        {
            timer.Stop();
            _manualPreview.CancelPending();
            _manualPreview.Close();
            _isPreviewMediaOpened = false;
            _pendingPreviewStartMs = 0d;
            _applyPendingStartOnPlay = false;
            _useLegacyPreviewFallback = false;
            uxPreviewImage.Stop();
            PlayerArea.Visibility = Visibility.Collapsed;
            PlayerController.Visibility = Visibility.Collapsed;
            uxPreviewImage.Visibility = Visibility.Collapsed;
            uxPreviewFallbackImage.Visibility = Visibility.Collapsed;
            uxPreviewImage.Source = null;
            uxPreviewFallbackImage.Source = null;
            IsPlaying = false;
        }

        private void ApplyManualPreviewTimerInterval()
        {
            timer.Interval = _useLegacyPreviewFallback
                ? _manualPreview.PlaybackInterval
                : TimeSpan.FromMilliseconds(100);
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
            SetPreviewPositionMs(tempSlider);
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

            if (_useLegacyPreviewFallback)
            {
                double nextMs = _manualPreview.PositionMs + timer.Interval.TotalMilliseconds;
                if (_manualPreview.DurationMs > 0d && nextMs > _manualPreview.DurationMs)
                {
                    nextMs = _manualPreview.DurationMs;
                    IsPlaying = false;
                }

                _manualPreview.SetPositionMs(nextMs, schedulePreview: false);
                _isUpdatingSliderFromPlayer = true;
                uxTimeSlider.Value = _manualPreview.PositionMs;
                _isUpdatingSliderFromPlayer = false;
                uxTime.Text = _manualPreview.PositionText;
                _ = _manualPreview.RefreshPreviewAsync();
                return;
            }

            if (!_isPreviewMediaOpened)
            {
                return;
            }

            double currentMs = GetPreviewPositionMs();
            if (_manualPreview.DurationMs > 0d && currentMs > _manualPreview.DurationMs)
            {
                currentMs = _manualPreview.DurationMs;
            }

            _manualPreview.SetPositionMs(currentMs, schedulePreview: false);
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxTimeSlider_DragEnter(object sender, DragEventArgs e)
        {
            isDragging = true;
        }

        private void UxTimeSlider_DragLeave(object sender, DragEventArgs e)
        {
            isDragging = false;
            SetPreviewPositionMs(uxTimeSlider.Value);
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxPreviewImage_MediaOpened(object sender, RoutedEventArgs e)
        {
            _useLegacyPreviewFallback = false;
            _isPreviewMediaOpened = true;
            _manualPreview.SetPositionMs(_pendingPreviewStartMs, schedulePreview: false);
            uxPreviewImage.Volume = uxVolumeSlider.Value;
            SetPreviewModeUi(useLegacyFallback: false);

            if (uxPreviewImage.NaturalDuration.HasTimeSpan)
            {
                _manualPreview.SetPositionMs(Math.Min(_pendingPreviewStartMs, uxPreviewImage.NaturalDuration.TimeSpan.TotalMilliseconds), schedulePreview: false);
                uxTimeSlider.Maximum = uxPreviewImage.NaturalDuration.TimeSpan.TotalMilliseconds;
            }

            uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            uxPreviewImage.Play();
            uxPreviewImage.Pause();
            uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            uxTime.Text = _manualPreview.PositionText;
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
        }

        private void UxPreviewImage_MediaEnded(object sender, RoutedEventArgs e)
        {
            IsPlaying = false;
            timer.Stop();
            _manualPreview.SetPositionMs(_manualPreview.DurationMs, schedulePreview: false);
            _isUpdatingSliderFromPlayer = true;
            uxTimeSlider.Value = _manualPreview.PositionMs;
            _isUpdatingSliderFromPlayer = false;
            uxTime.Text = _manualPreview.PositionText;
        }

        private void UxPreviewImage_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            IsPlaying = false;
            timer.Stop();
            _isPreviewMediaOpened = false;
            ActivateLegacyPreviewFallback();
        }

        private double GetPreviewPositionMs() =>
            _isPreviewMediaOpened
                ? uxPreviewImage.Position.TotalMilliseconds
                : _manualPreview.PositionMs;

        private void SetPreviewPositionMs(double positionMs)
        {
            _manualPreview.SetPositionMs(positionMs, schedulePreview: false);
            _pendingPreviewStartMs = _manualPreview.PositionMs;
            _applyPendingStartOnPlay = false;
            if (_useLegacyPreviewFallback)
            {
                _ = _manualPreview.RefreshPreviewAsync();
                return;
            }

            if (_isPreviewMediaOpened)
            {
                uxPreviewImage.Position = TimeSpan.FromMilliseconds(_manualPreview.PositionMs);
            }
        }

        private void ActivateLegacyPreviewFallback()
        {
            _useLegacyPreviewFallback = true;
            _applyPendingStartOnPlay = false;
            uxPreviewImage.Stop();
            SetPreviewModeUi(useLegacyFallback: true);
            ApplyManualPreviewTimerInterval();
            _ = _manualPreview.RefreshPreviewAsync();
        }

        private void SetPreviewModeUi(bool useLegacyFallback)
        {
            uxPreviewImage.Visibility = useLegacyFallback ? Visibility.Collapsed : Visibility.Visible;
            uxPreviewFallbackImage.Visibility = useLegacyFallback ? Visibility.Visible : Visibility.Collapsed;
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