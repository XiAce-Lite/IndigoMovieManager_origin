using AvalonDock;
using AvalonDock.Layout.Serialization;
using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;
using IndigoMovieManager.Data;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
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
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.SQLite;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IMainWindowActions, IMainWindowListHost
    {
        //監視モードは FolderCheckMode（Services/FolderCheckService.cs）を使用
        private Task _processorTask;
        private readonly CancellationTokenSource _processorCts = new();
        private readonly ThumbnailWorkScope _thumbnailWorkScope = new();
        private readonly ThumbnailQueueProcessor _thumbnailQueueProcessor = new();
        private readonly ThumbnailQueueScheduler _thumbnailScheduler = new();
        private readonly FileWatcherManager _fileWatcherManager = new();
        private readonly DiscoveredFileRegistrationGate _discoveredFileRegistrationGate = new();
        private readonly List<QueueObj> _pendingDiscoveredThumbnailWork = [];
        private readonly object _pendingDiscoveredThumbnailLock = new();
        private CancellationTokenSource _discoveredThumbnailFlushCts;
        private int _discoveredRegistrationInFlight;
        private const int DiscoveredThumbnailFlushDelayMs = 1500;
        private readonly SemaphoreSlim _folderCheckGate = new(1, 1);
        private bool _openingDatabase;
        private bool _suppressSkinComboChange;
        private bool _suppressSkinModeChange;
        private SkinEngine _currentSkinEngine = SkinEngine.Wpf;
        private DispatcherTimer _wbDrawerRestoreTimer;
        private const string SkinEngineWpf = SkinEngineHelper.SettingWpf;
        private const string SkinEngineWb = SkinEngineHelper.SettingWb;

        private Stack<string> recentFiles = new();

        private IEnumerable<MovieRecords> filterList = [];

        private StatusBarProgressCoordinator.ThumbnailSlotHandle _thumbnailScanHandle;

        private string _cachedAllItemsSortId;
        private List<MovieRecords> _cachedAllItems;
        private int _cachedAllItemsSourceCount;

        private DataTable systemData;
        private bool _movieRecordsLoaded;
        private readonly MovieListCoordinator _movieListCoordinator = new();
        private DataTable historyData;
        private DataTable watchData;
        private DataTable bookmarkData;
        private DataTable tagBarData;
        private readonly HashSet<string> _bookmarkThumbInFlight = new(StringComparer.OrdinalIgnoreCase);

        // MainWindow クラス内の MainVM フィールドまたはプロパティの宣言を public に変更
        public readonly MainWindowViewModel MainVM;
        internal System.Windows.Point lbClickPoint = new();

        private DateTime _lastSliderTime = DateTime.MinValue;
        private readonly TimeSpan _timeSliderInterval = TimeSpan.FromSeconds(0.1);

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

        private System.Windows.Point _skinThumbClickOnImage;
        private double _skinThumbImageWidth;
        private double _skinThumbImageHeight;
        private bool _skinThumbClickValid;
        private long _skinThumbClickMovieId;

        //IME起動中的なフラグ。日本語入力中（未変換）にインクリメンタルサーチさせない為。
        private bool _imeFlag = false;

        private readonly ThumbnailLayoutCache _thumbLayoutCache = new();
        private readonly MainWindowSessionState _sessionState = new();
        private readonly StatusBarProgressCoordinator _statusBarProgress;

        private bool _isDeletingSearchHistory = false;
        private bool _isApplyingSearchKeyword = false;
        // 検索ボックスへユーザーが入力した内容のみ LostFocus で履歴化する（TagBar 等は対象外）。
        private bool _pendingTypedSearchHistory = false;
        // 検索キーワード変更時のサムネ生成スコープ判定用（ソート変更のみでは触らない）。
        private string _lastThumbnailScopeSearchKeyword = "";
        // 検索履歴ドロップダウンのキーボードカーソル位置（SelectedIndex はTextバインドで-1にリセットされ得るため独自管理）。
        private int _historyCursor = -1;
        private int _fileInfoRefreshRunning = 0;
        private int _dmmFetchRunning = 0;
        private DmmAutoFetchQueue _dmmAutoFetchQueue;

        private const int SearchOverlayDelayMs = 400;
        private const int SearchIncrementalDebounceMs = 400;
        private int _loadingOverlayDepth;
        private CancellationTokenSource _searchOverlayDelayCts;
        private CancellationTokenSource _searchIncrementalDebounceCts;
        private bool _searchOverlayPushed;

        public MainWindow()
        {
            MainVM = new MainWindowViewModel(); // ← 追加
            
            Properties.SettingsUpgrader.TryUpgrade(Properties.Settings.Default);

            recentFiles.Clear();

            InitializeComponent();

            AppThemeService.ApplyDockTheme(uxDockingManager);
            AppThemeService.ApplyHeaderZone(HeaderZone);
            AppThemeService.ThemeChanged += OnAppThemeChanged;

            _statusBarProgress = new StatusBarProgressCoordinator(Dispatcher);
            StatusBarProgressHost.Attach(_statusBarProgress);
            OperationStatusBar.DataContext = _statusBarProgress.ViewModel;
            _dmmAutoFetchQueue = new DmmAutoFetchQueue(new MainWindowDmmAutoFetchHost(this));

            // アセンブリのファイルバージョンを取得
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

            // ビルドの取り違え防止のため、実行ファイルのビルド時刻も表示する。
            this.Title = $"Indigo Movie Manager v{version} build {GetBuildStamp()}";

            ContentRendered += MainWindow_ContentRendered;
            Closing += MainWindow_Closing;
            TextCompositionManager.AddPreviewTextInputHandler(SearchBox, OnPreviewTextInput);
            TextCompositionManager.AddPreviewTextInputStartHandler(SearchBox, OnPreviewTextInputStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(SearchBox, OnPreviewTextInputUpdate);

            if (Properties.Settings.Default.RecentFiles != null)
            {
                recentFiles = RecentFilesService.LoadFromSettings(Properties.Settings.Default.RecentFiles);
                SyncRecentFilesUi();
            }
            else
            {
                JumpListService.SyncRecentFiles(recentFiles);
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

            SkinViewGridWb.PlayRequested += SkinView_PlayRequested;
            SkinViewGridWb.SearchTagRequested += SkinView_SearchTagRequested;
            SkinViewGridWb.RemoveTagRequested += SkinView_RemoveTagRequested;

            string savedWbSkin = Properties.Settings.Default.LastWbSkinFolder;
            if (!string.IsNullOrWhiteSpace(savedWbSkin)
                && WhiteBrowserSkinSettings.EnumerateSkinFolders().Contains(savedWbSkin, StringComparer.OrdinalIgnoreCase))
            {
                WhiteBrowserSkinSettings.ActiveSkinFolder = savedWbSkin;
            }

            string savedWpfSkin = Properties.Settings.Default.LastWpfSkinName;
            ApplyWpfSkin(string.IsNullOrWhiteSpace(savedWpfSkin) ? null : savedWpfSkin);
            UpdateWbSkinTabTag();
            SkinEngine initialEngine = GetSavedSkinEngine();
            ApplySkinEngineVisibility(initialEngine);
            SwitchSkinEngine(initialEngine, refreshList: false);
            UpdateSkinToolbar(initialEngine);

            // WebView2 はネイティブ HWND のため WPF オーバーレイより前面に出る（エアスペース問題）。
            // ドロワー（ハンバーガーメニュー）表示中は SkinView を隠して被りを防ぐ。
            MenuToggleButton.Checked += MenuToggleButton_DrawerStateChanged;
            MenuToggleButton.Unchecked += MenuToggleButton_DrawerStateChanged;
        }

        private void OnAppThemeChanged(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnAppThemeChanged(sender, e));
                return;
            }

            AppThemeService.ApplyDockTheme(uxDockingManager);
            AppThemeService.ApplyHeaderZone(HeaderZone);
            if (_wpfSkin != null)
            {
                ApplyWpfSkin(GetCurrentWpfSkinFolder());
                WpfSkinList.Items.Refresh();
                Dispatcher.BeginInvoke(ReapplyWpfSkinListSurface, System.Windows.Threading.DispatcherPriority.Loaded);
                Dispatcher.BeginInvoke(ReapplyWpfSkinListSurface, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void UpdateWbSkinTabTag() =>
            WbSkinHost.Tag = WhiteBrowserSkinSettings.GetThumbnailTag();

        private static bool IsWbEngine(string value) =>
            string.Equals(value, SkinEngineWb, StringComparison.OrdinalIgnoreCase);

        private SkinEngine GetSavedSkinEngine() =>
            SkinEngineHelper.FromSetting(Properties.Settings.Default.LastSkinEngine);

        private void SaveSkinEngine(SkinEngine engine)
        {
            AppSettingsPersistence.SaveSkinEngineIfChanged(SkinEngineHelper.ToSetting(engine));
        }

        private void ApplySkinEngineVisibility(SkinEngine engine)
        {
            bool isWpf = engine == SkinEngine.Wpf;
            WpfSkinHost.Visibility = isWpf ? Visibility.Visible : Visibility.Collapsed;
            WbSkinHost.Visibility = isWpf ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// 共通スキンツールバー（方式トグル＋スキン Combo）を、選択中エンジンに合わせて更新する。
        /// </summary>
        private void UpdateSkinToolbar(SkinEngine engine)
        {
            if (HeaderZone == null)
            {
                return;
            }

            SaveSkinEngine(engine);

            _suppressSkinModeChange = true;
            ModeWpfRadio.IsChecked = engine == SkinEngine.Wpf;
            ModeWbRadio.IsChecked = engine == SkinEngine.Wb;
            _suppressSkinModeChange = false;

            bool isWpf = engine == SkinEngine.Wpf;
            ReloadSkinButton.Visibility = isWpf ? Visibility.Visible : Visibility.Hidden;
            ReloadSkinButton.IsEnabled = isWpf;

            RebuildSkinCombo(engine == SkinEngine.Wpf);
        }

        private IReadOnlyList<MovieRecords> GetActiveFilterRecords()
        {
            if (filterList == null)
            {
                return [];
            }

            return filterList as IReadOnlyList<MovieRecords> ?? [.. filterList];
        }

        private void RestartThumbnailsForActiveFilter(bool useFullLibrary = false)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            ThumbnailLayoutSpec layout = ThumbnailLayoutResolver.GetActiveListLayout(_currentSkinEngine);
            if (layout == null)
            {
                return;
            }

            // 表示は常に現在のフィルタ結果（ランダム等の並びを維持）。
            // useFullLibrary はサムネ生成キューの対象だけライブラリ全体にする。
            IReadOnlyList<MovieRecords> displayRecords = GetActiveFilterRecords();
            IReadOnlyList<MovieRecords> thumbRecords = useFullLibrary
                ? MainVM.MovieRecs as IReadOnlyList<MovieRecords> ?? [.. MainVM.MovieRecs]
                : displayRecords;

            // スキン切替時は対象0件でも直前ジョブを必ず止める（{::error} 0件→別スキン→0件戻し等）
            AbandonTabThumbnailWork(layout.Key);
            EndThumbnailScanProgress();
            ThumbnailQueueProcessor.RequestDismissProgress();

            ThumbPathHelper.ResolveThumbPathsForEngine(thumbRecords, _thumbLayoutCache, _currentSkinEngine);

            if (_currentSkinEngine == SkinEngine.Wb)
            {
                SkinViewGridWb.Tag = displayRecords;
                SkinViewGridWb.RenderItems(displayRecords);
            }
            else
            {
                WpfSkinList.ItemsSource = displayRecords;
            }

            if (thumbRecords.Count == 0)
            {
                return;
            }

            string skinName = GetActiveSkinDisplayName();
            bool showScanProgress = thumbRecords.Count > 64;

            StartTabSwitchThumbnailJob(
                layout,
                thumbRecords,
                skinName,
                showScanProgress: showScanProgress,
                skipAbandon: true,
                onFirstBatchEnqueued: () => RunOnUi(EndThumbnailScanProgress),
                onScanCompleted: () => RunOnUi(EndThumbnailScanProgress));
        }

        private string GetActiveSkinDisplayName() =>
            _currentSkinEngine == SkinEngine.Wb
                ? WhiteBrowserSkinSettings.ActiveSkinFolder
                // 進捗表示はコンボと同じフォルダ名（name は Save As 後も元スキンのまま残りやすい）
                : _wpfSkin?.FolderName ?? _wpfSkin?.Name ?? "Indigo";

        private bool ShouldUseFullLibraryForThumbnailRestart() =>
            string.IsNullOrWhiteSpace(MainVM.DbInfo.SearchKeyword);

        private async Task OnSkinLayoutChangedAsync()
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(MainVM.DbInfo.SearchKeyword))
            {
                await ApplyFilterAndSortAsync(MainVM.DbInfo.Sort ?? "1").ConfigureAwait(true);
            }

            RestartThumbnailsForActiveFilter(useFullLibrary: ShouldUseFullLibraryForThumbnailRestart());
        }

        private void BeginThumbnailScanProgress(string skinName)
        {
            EndThumbnailScanProgress();
            string title = $"サムネイル確認中 ({skinName})";
            _thumbnailScanHandle = StatusBarProgressHost.Coordinator.BeginThumbnail(title);
            _thumbnailScanHandle.Report(title, 0, "未作成サムネイルを検索しています…");
        }

        private void EndThumbnailScanProgress()
        {
            _thumbnailScanHandle?.Dispose();
            _thumbnailScanHandle = null;
        }

        private void SwitchSkinEngine(SkinEngine engine, bool refreshList = true)
        {
            _currentSkinEngine = engine;
            MainVM.DbInfo.CurrentSkinEngine = engine;
            ApplySkinEngineVisibility(engine);
            UpdateSkinToolbar(engine);

            if (!refreshList || _openingDatabase)
            {
                return;
            }

            _ = OnSkinLayoutChangedAsync();
            SelectFirstItem();
        }

        private void RebuildSkinCombo(bool isWpf)
        {
            _suppressSkinComboChange = true;
            if (isWpf)
            {
                IReadOnlyList<string> skins = Services.WpfSkin.WpfSkinLoader.EnumerateSkins();
                ComboSkin.ItemsSource = skins;
                ComboSkin.SelectedItem = skins.Contains(GetCurrentWpfSkinFolder())
                    ? GetCurrentWpfSkinFolder()
                    : skins.FirstOrDefault();
            }
            else
            {
                IReadOnlyList<string> skins = WhiteBrowserSkinSettings.EnumerateSkinFolders();
                ComboSkin.ItemsSource = skins;
                string active = WhiteBrowserSkinSettings.ActiveSkinFolder;
                ComboSkin.SelectedItem = skins.Contains(active) ? active : skins.FirstOrDefault();
            }
            _suppressSkinComboChange = false;
        }

        private void SkinModeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSkinModeChange)
            {
                return;
            }

            SkinEngine target = ReferenceEquals(sender, ModeWpfRadio)
                ? SkinEngine.Wpf
                : SkinEngine.Wb;

            if (_currentSkinEngine != target)
            {
                SwitchSkinEngine(target);
            }
            else
            {
                SaveSkinEngine(target);
                UpdateSkinToolbar(target);
            }
        }

        private async void ComboSkin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSkinComboChange)
            {
                return;
            }

            if (ComboSkin.SelectedItem is not string name)
            {
                return;
            }

            if (ModeWbRadio.IsChecked == true)
            {
                await ApplyWbSkinSelectionAsync(name).ConfigureAwait(true);
            }
            else
            {
                await ApplyWpfSkinSelectionAsync(name).ConfigureAwait(true);
            }
        }

        private async Task ApplyWbSkinSelectionAsync(string folder)
        {
            if (string.Equals(folder, WhiteBrowserSkinSettings.ActiveSkinFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            WhiteBrowserSkinSettings.ActiveSkinFolder = folder;
            AppSettingsPersistence.SaveWbSkinSelection(SkinEngineWb, folder);
            UpdateWbSkinTabTag();

            try
            {
                await SkinViewGridWb.ReloadWhiteBrowserSkinAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"WBスキンの読み込みに失敗しました: {ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentSkinEngine != SkinEngine.Wb)
            {
                return;
            }

            _ = OnSkinLayoutChangedAsync();
        }

        private static string MapLegacySkinToWpfSkinName(string skin) =>
            string.IsNullOrWhiteSpace(skin)
                ? null
                : skin.Replace(" ", "") switch
                {
                    "DefaultSmall" => "DefaultSmall",
                    "DefaultBig" => "DefaultBig",
                    "DefaultGrid" => "DefaultGrid",
                    "DefaultList" => "DefaultList",
                    "DefaultBig10" => "DefaultBig10",
                    _ => skin,
                };

        private SkinEngine PrepareStartupSkinMode(string dbSkin)
        {
            SkinEngine engine = GetSavedSkinEngine();
            if (engine == SkinEngine.Wpf)
            {
                string mappedSkin = MapLegacySkinToWpfSkinName(dbSkin);
                string skinName = !string.IsNullOrWhiteSpace(mappedSkin)
                    ? mappedSkin
                    : Properties.Settings.Default.LastWpfSkinName;
                ApplyWpfSkin(string.IsNullOrWhiteSpace(skinName) ? null : skinName);

                if (!string.IsNullOrWhiteSpace(GetCurrentWpfSkinFolder()))
                {
                    Properties.Settings.Default.LastWpfSkinName = GetCurrentWpfSkinFolder();
                }
            }

            _currentSkinEngine = engine;
            MainVM.DbInfo.CurrentSkinEngine = engine;
            ApplySkinEngineVisibility(engine);
            UpdateSkinToolbar(engine);
            return engine;
        }

        private void MenuToggleButton_DrawerStateChanged(object sender, RoutedEventArgs e)
        {
            _wbDrawerRestoreTimer?.Stop();

            if (MenuToggleButton.IsChecked == true)
            {
                SkinViewGridWb.Visibility = Visibility.Hidden;
                return;
            }

            _wbDrawerRestoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            _wbDrawerRestoreTimer.Tick += OnWbDrawerRestoreTimerTick;
            _wbDrawerRestoreTimer.Start();
        }

        private void OnWbDrawerRestoreTimerTick(object sender, EventArgs e)
        {
            _wbDrawerRestoreTimer?.Stop();
            if (MenuToggleButton.IsChecked != true)
            {
                SkinViewGridWb.Visibility = Visibility.Visible;
            }
        }

        private Services.WpfSkin.WpfSkinDefinition _wpfSkin;

        private async Task ApplyWpfSkinSelectionAsync(string folder)
        {
            ApplyWpfSkin(folder);
            AppSettingsPersistence.SaveWpfSkinSelection(SkinEngineWpf, folder);
            await OnSkinLayoutChangedAsync().ConfigureAwait(true);
        }

        private void ReloadSkin_Click(object sender, RoutedEventArgs e)
        {
            string skinName = ComboSkin.SelectedItem as string ?? GetCurrentWpfSkinFolder();
            ApplyWpfSkin(skinName);
            _ = OnSkinLayoutChangedAsync();
        }

        private void RefreshWpfSkinItemsForCurrentFilter()
        {
            if (_currentSkinEngine == SkinEngine.Wpf)
            {
                _ = OnSkinLayoutChangedAsync();
            }
        }

        /// <summary>skin.json から WPF ネイティブスキンの ItemsPanel / ItemTemplate を組み立てて適用する。</summary>
        private void ApplyWpfSkin(string skinName = null)
        {
            // 旧スキンのジャケ写取得を捨て、ステータスバー件数もリセット
            Services.Dmm.DmmRemoteImageLoader.CancelPendingAndResetProgress();

            _wpfSkin = skinName != null && Services.WpfSkin.WpfSkinLoader.TryLoad(skinName, out var def)
                ? def
                : Services.WpfSkin.WpfSkinLoader.LoadDefault();

            Services.WpfSkin.WpfSkinSettings.CurrentThumbnailLayout =
                Thumbnail.ThumbnailLayoutSpec.FromWpfSkinThumbnail(_wpfSkin.Thumbnail);
            Services.WpfSkin.WpfSkinSettings.PreferJacket = _wpfSkin.Thumbnail?.PreferJacket == true;

            var context = new Services.WpfSkin.WpfSkinTemplateBuilder.BuildContext
            {
                ItemContextMenu = FindResource("menuContext") as ContextMenu,
                ThumbnailDoubleClick = new MouseButtonEventHandler((s, e) => PlayMovie_Click(s, e)),
                ThumbnailMouseDown = new MouseButtonEventHandler(Label_MouseDown),
                ThumbnailRightDown = new MouseButtonEventHandler(ThumbnailImage_PreviewMouseRightButtonDown),
                ImageConverter = new Converter.NoLockImageConverter(),
                AspectConverter = new Converter.AspectStretchConverter(),
                FileSizeConverter = new Converter.FileSizeConverter(),
                PathLinkClick = OpenWpfSkinPathLink,
            };

            Services.WpfSkin.WpfSkinTemplateBuilder.ApplyHostContext(context);

            WpfSkinList.ItemsPanel = Services.WpfSkin.WpfSkinTemplateBuilder.BuildItemsPanel(_wpfSkin);
            WpfSkinList.ItemTemplate = Services.WpfSkin.WpfSkinTemplateBuilder.BuildItemTemplate(_wpfSkin);
            WpfSkinList.ItemContainerStyle = BuildWpfSkinItemContainerStyle(_wpfSkin);

            // list 型は横スクロール可・カラム見出し行を表示。card 型は従来通り横スクロール無し。
            ScrollViewer.SetHorizontalScrollBarVisibility(
                WpfSkinList,
                _wpfSkin.IsList ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);

            UIElement header = Services.WpfSkin.WpfSkinLayoutBuilder.BuildListHeader(_wpfSkin);
            WpfSkinHeaderHost.Content = header;
            WpfSkinHeaderScroll.Visibility = header != null ? Visibility.Visible : Visibility.Collapsed;

            System.Windows.Media.Brush surfaceBg = Services.WpfSkin.WpfSkinTemplateBuilder.ParseSurfaceBackground(_wpfSkin);
            Services.WpfSkin.WpfSkinListChrome.ApplySurface(WpfSkinList, surfaceBg, WpfSkinHost, MovieListHost);
        }

        /// <summary>コンボ／設定用のフォルダキー。表示名 <see cref="Services.WpfSkin.WpfSkinDefinition.Name"/> とは別。</summary>
        private string GetCurrentWpfSkinFolder() =>
            !string.IsNullOrWhiteSpace(_wpfSkin?.FolderName)
                ? _wpfSkin.FolderName
                : _wpfSkin?.Name;

        private void ReapplyWpfSkinListSurface()
        {
            if (_wpfSkin == null)
            {
                return;
            }

            System.Windows.Media.Brush surfaceBg = Services.WpfSkin.WpfSkinTemplateBuilder.ParseSurfaceBackground(_wpfSkin);
            Services.WpfSkin.WpfSkinListChrome.ApplySurface(WpfSkinList, surfaceBg, WpfSkinHost, MovieListHost);
        }

        // ListViewItem スタイルをスキンに合わせて生成する。
        // stretch スキン（既定 Big/5x10 相当）はアイテムを全幅に伸ばし、選択ハイライトを
        // ウィンドウ幅いっぱいに出す。それ以外は従来どおり左寄せ・自然幅のまま。
        private Style BuildWpfSkinItemContainerStyle(Services.WpfSkin.WpfSkinDefinition def)
        {
            bool stretch = def?.Card?.Stretch == true;
            HorizontalAlignment hAlign = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

            var style = new Style(typeof(ListViewItem));
            style.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
            style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, hAlign));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, hAlign));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Top));
            style.Setters.Add(new EventSetter(
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(WpfSkinItem_PreviewMouseLeftButtonDown)));

            var selectedTrigger = new Trigger
            {
                Property = ListViewItem.IsSelectedProperty,
                Value = true,
            };
            selectedTrigger.Setters.Add(new Setter(
                Control.BackgroundProperty,
                ResolveListItemSelectedBackground(def)));
            style.Triggers.Add(selectedTrigger);

            return style;
        }

        private static System.Windows.Media.Brush ResolveListItemSelectedBackground(Services.WpfSkin.WpfSkinDefinition def)
        {
            if (Services.WpfSkin.WpfSkinColorResolver.IsJsonAuthoritative(def)
                && string.Equals(def.ColorProfile, "dark", StringComparison.OrdinalIgnoreCase))
            {
                return Application.Current.TryFindResource("ImmListItemSelectedBackgroundDarkSkin") as System.Windows.Media.Brush
                    ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x37, 0x47, 0x4F));
            }

            return Application.Current.TryFindResource("ImmListItemSelectedBackground") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.LightSteelBlue;
        }

        // リスト型スキンのヘッダー行を本体の横スクロールに追従させる。
        private void WpfSkinList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (WpfSkinHeaderScroll.Visibility == Visibility.Visible)
            {
                WpfSkinHeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
        }

        // リスト型で横スクロールバーが出るとき、Shift+ホイールを横スクロールとする。
        private void WpfSkinList_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
            ListScrollPreserver.TryHandleShiftMouseWheel(WpfSkinList, e);

        // WPF スキンタブのカード内要素クリック時に選択状態にする（ネイティブタブと同じ挙動）。
        private void WpfSkinItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    return;
                }

                if (!item.IsSelected)
                {
                    item.IsSelected = true;
                    WpfSkinList.SelectedItem = item.DataContext;
                }
            }
        }


        // 実行ファイルの最終更新時刻をビルド識別子として返す（単一ファイル発行でも取得可）。
        private static string GetBuildStamp()
        {
            try
            {
                string path = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return File.GetLastWriteTime(path).ToString("yyyyMMdd-HHmmss");
                }
            }
            catch
            {
            }

            return "unknown";
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

                // 起動引数の .wb を優先。無ければ AutoOpen + LastDoc。
                if (!TryOpenStartupDocument())
                {
                    if (Properties.Settings.Default.AutoOpen
                        && !string.IsNullOrEmpty(Properties.Settings.Default.LastDoc)
                        && Path.Exists(Properties.Settings.Default.LastDoc))
                    {
                        _ = OpenDatafileAsync(Properties.Settings.Default.LastDoc);
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

            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                Version current = UpdateCheckService.GetCurrentVersion();
                UpdateCheckService.ReleaseInfo newer =
                    await UpdateCheckService.TryGetNewerReleaseAsync(current).ConfigureAwait(true);
                if (newer == null)
                {
                    return;
                }

                string dismissed = Properties.Settings.Default.DismissedUpdateVersion ?? "";
                if (string.Equals(dismissed, newer.Version.ToString(), StringComparison.Ordinal))
                {
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    this,
                    $"新しいバージョン {newer.Version} が公開されています。\n" +
                    $"現在のバージョン: {current}\n\n" +
                    "リリースページを開きますか？\n" +
                    "（「いいえ」を選ぶと、このバージョンについては再通知しません）",
                    "アップデートのお知らせ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(newer.HtmlUrl) { UseShellExecute = true });
                }

                Properties.Settings.Default.DismissedUpdateVersion = newer.Version.ToString();
                Properties.Settings.Default.Save();
            }
            catch
            {
                // オフライン等では黙ってスキップする。
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
                _dmmAutoFetchQueue?.Dispose();
                Properties.Settings.Default.MainLocation = new System.Drawing.Point((int)Left, (int)Top);
                Properties.Settings.Default.MainSize = new System.Drawing.Size((int)Width, (int)Height);

                AppSettingsPersistence.SaveRecentFiles(recentFiles.Reverse());

                XmlLayoutSerializer layoutSerializer = new(uxDockingManager);
                using var writer = new StreamWriter(ApplicationPaths.LayoutFilePath);
                layoutSerializer.Serialize(writer);

                // DB 未オープン時は system テーブルへ触れない（空 Data Source で GetData が落ちる）。
                if (!string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                {
                    UpdateSkin();
                    UpdateSort();

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

        private void CancelActiveThumbnailWork()
        {
            ThumbnailQueueProcessor.RequestDismissProgress();
            _thumbnailWorkScope.CancelBatch();
            string layoutKey = GetActiveListLayoutKey();
            // DB 切替・全取消: 他スキン先作りも含めて破棄
            _thumbnailScheduler.AbandonAndClearQueue(layoutKey, preserveRetainedWork: false);
            _thumbnailScheduler.ClearTrackingForLayoutKey(layoutKey);
            _sessionState.BumpThumbnailWorkGeneration();
        }

        private void AbandonThumbnailWorkForDbSwitch() =>
            CancelActiveThumbnailWork();

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

        private void EnqueueThumbnailWork(IReadOnlyList<QueueObj> items, bool beginNewJob = false)
        {
            StampQueueDbContext(items);
            ApplyActiveThumbnailLayout(items, _currentSkinEngine);
            _thumbnailScheduler.EnqueueWork(items, GetActiveListLayoutKey(), beginNewJob);
        }

        private void EnqueueThumbnailWork(QueueObj item, bool beginNewJob = false)
        {
            StampQueueDbContext(item);
            ApplyActiveThumbnailLayout(item);
            _thumbnailScheduler.EnqueueWork(item, GetActiveListLayoutKey(), beginNewJob);
        }

        private static void ApplyActiveThumbnailLayout(QueueObj item, SkinEngine engine)
        {
            if (item == null || item.ThumbnailLayout != null)
            {
                return;
            }

            Thumbnail.ThumbnailLayoutSpec spec = ThumbnailLayoutResolver.GetActiveListLayout(engine);
            if (spec == null)
            {
                return;
            }

            item.ThumbnailLayout = spec;
        }

        private static void ApplyActiveThumbnailLayout(IEnumerable<QueueObj> items, SkinEngine engine)
        {
            if (items == null)
            {
                return;
            }

            foreach (QueueObj item in items)
            {
                ApplyActiveThumbnailLayout(item, engine);
            }
        }

        private void ApplyActiveThumbnailLayout(QueueObj item) =>
            ApplyActiveThumbnailLayout(item, _currentSkinEngine);


        private void EnsureDetailThumbnail(MovieRecords mv, bool forceRecreate = false)
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
            string expectedDetailPath = _thumbLayoutCache.GetExpectedDetailThumbPath(movieBody, hash);

            ThumbnailLayoutSpec listLayout = ThumbnailLayoutResolver.GetActiveListLayout(_currentSkinEngine);
            ThumbnailLayoutSpec detailFallback = listLayout.DivCount == 1 ? listLayout : null;
            mv.ThumbDetail = _thumbLayoutCache.ResolveDetailThumbPath(thumbFile, checkExists: true, detailFallback);

            if (!forceRecreate
                && (ZipMediaKind.IsZipRecord(mv) || ZipMediaKind.IsZipPath(mv.Movie_Path)))
            {
                if (ZipDetailThumbnailMaterializer.TryCopyFromExistingListThumbs(
                        _thumbLayoutCache,
                        movieBody,
                        hash,
                        expectedDetailPath))
                {
                    mv.ThumbDetail = expectedDetailPath;
                    return;
                }
            }

            string detailLayoutKey = ThumbnailLayoutSpec.DetailPaneLayout.Key;
            if (_thumbnailScheduler.JobCoordinator.IsInFlight(mv.Movie_Id, detailLayoutKey))
            {
                return;
            }

            if (!forceRecreate
                && !ThumbnailTabErrorDetector.IsDetailThumbnailError(
                    mv,
                    _thumbLayoutCache,
                    CreateThumbnailHashSyncContext()))
            {
                return;
            }

            if (forceRecreate)
            {
                _thumbnailScheduler.JobCoordinator.UntrackIfNotInFlight(mv.Movie_Id, detailLayoutKey);
            }

            var item = new QueueObj
            {
                MovieId = mv.Movie_Id,
                MovieFullPath = mv.Movie_Path,
                ThumbnailLayout = ThumbnailLayoutSpec.DetailPaneLayout,
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
            // WPF スキンタブの手動サムネも、自動と同じ動的レイアウト（W×H×C×R）で
            // 正しい出力フォルダ・サイズへ保存させる。
            ApplyActiveThumbnailLayout(item);
            return _thumbnailScheduler.TryEnqueueManualWork(item);
        }

        private void CancelThumbnailWorkForMovie(long movieId) =>
            _thumbnailScheduler.CancelTrackedForMovie(movieId);

        private ThumbnailLayoutSpec GetActiveListLayout() =>
            ThumbnailLayoutResolver.GetActiveListLayout(_currentSkinEngine);

        private void PopulateActiveListQueueLayout(QueueObj item)
        {
            if (item == null)
            {
                return;
            }

            ThumbnailLayoutSpec layout = GetActiveListLayout();
            if (layout != null)
            {
                item.ThumbnailLayout = layout;
            }
        }

        /// <summary>
        /// 新規登録バッチ向け。選んだ他スキン用レイアウトを retained silent で投入する。
        /// 可視ジョブに載せるとスキン切替の Abandon で一緒に捨てられるため、切替耐性のある silent にする。
        /// アクティブレイアウトは通常ジョブ側。既存ライブラリの穴埋めはしない。
        /// </summary>
        private void EnqueueExtraSkinThumbsForNewMovies(IReadOnlyList<QueueObj> newlyAdded)
        {
            if (newlyAdded == null
                || newlyAdded.Count == 0
                || !MainVM.DbInfo.PreGenThumbsOnNewMovies)
            {
                return;
            }

            string activeKey = GetActiveListLayoutKey();
            if (string.IsNullOrWhiteSpace(activeKey))
            {
                return;
            }

            HashSet<string> selectedKeys = PreGenThumbSkinSelection.FilterExistingSelectedKeys(
                PreGenThumbSkinSelection.ParseStoredKeys(MainVM.DbInfo.PreGenThumbSkinKeys));
            if (selectedKeys.Count == 0)
            {
                return;
            }

            IReadOnlyList<ThumbnailLayoutSpec> layouts =
                PreGenThumbSkinSelection.ResolveUniqueLayouts(selectedKeys)
                    .Where(layout => layout != null
                        && !string.Equals(layout.Key, activeKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            if (layouts.Count == 0)
            {
                return;
            }

            var extras = new List<QueueObj>();
            foreach (QueueObj source in newlyAdded)
            {
                if (source == null || source.MovieId <= 0 || string.IsNullOrWhiteSpace(source.MovieFullPath))
                {
                    continue;
                }

                if (!File.Exists(source.MovieFullPath))
                {
                    continue;
                }

                MovieRecords record = MainVM.MovieRecs?.FirstOrDefault(r => r.Movie_Id == source.MovieId);
                string hash = record?.Hash?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(hash))
                {
                    hash = GetHashCRC32(source.MovieFullPath) ?? string.Empty;
                }

                if (string.IsNullOrEmpty(hash))
                {
                    continue;
                }

                string fileBody = Path.GetFileNameWithoutExtension(source.MovieFullPath).ToLowerInvariant();
                string dbFullPath = source.DbFullPath ?? MainVM.DbInfo.DBFullPath;

                foreach (ThumbnailLayoutSpec layout in layouts)
                {
                    string expectedPath = _thumbLayoutCache.GetExpectedThumbPath(layout, fileBody, hash);
                    if (!string.IsNullOrWhiteSpace(expectedPath) && File.Exists(expectedPath))
                    {
                        // error プレースホルダだけ作り直す
                        if (!ThumbnailHashSync.IsLikelyErrorPlaceholder(expectedPath, _thumbLayoutCache))
                        {
                            continue;
                        }
                    }

                    extras.Add(new QueueObj
                    {
                        MovieId = source.MovieId,
                        MovieFullPath = source.MovieFullPath,
                        DbFullPath = dbFullPath,
                        ThumbnailLayout = layout,
                        RetainAcrossLayoutSwitch = true,
                    });
                }
            }

            if (extras.Count == 0)
            {
                return;
            }

            // スキン切替の Abandon / ClearSilent でも残る silent として投入（進捗バー外）
            StampQueueDbContext(extras);
            _thumbnailScheduler.EnqueueSilentWork(extras);
        }

        private string GetActiveListLayoutKey() =>
            GetActiveListLayout()?.Key ?? "";

        private bool IsMovieListActive => !string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath);

        private void StartTabSwitchThumbnailJob(
            ThumbnailLayoutSpec layout,
            IReadOnlyList<MovieRecords> records = null,
            string displayTitle = null,
            bool showScanProgress = false,
            bool skipAbandon = false,
            Action onFirstBatchEnqueued = null,
            Action onScanCompleted = null)
        {
            IReadOnlyList<MovieRecords> target = records ?? GetActiveFilterRecords();
            if (layout == null || target.Count == 0)
            {
                onScanCompleted?.Invoke();
                return;
            }

            if (!skipAbandon)
            {
                AbandonTabThumbnailWork(layout.Key);
            }

            if (showScanProgress)
            {
                BeginThumbnailScanProgress(displayTitle ?? GetActiveSkinDisplayName());
            }

            int buildEpoch = _thumbnailScheduler.TabSwitchBuildGeneration;

            _thumbnailScheduler.StartTabSwitchJob(
                layout,
                target,
                _thumbLayoutCache,
                MainVM.DbInfo.DBFullPath,
                _sessionState.ThumbnailWorkGeneration,
                buildEpoch,
                displayTitle ?? GetActiveSkinDisplayName(),
                onFirstBatchEnqueued,
                onScanCompleted);
        }

        private void AbandonTabThumbnailWork(string layoutKey)
        {
            _thumbnailScheduler.AbandonAndClearQueue(layoutKey);
            _thumbnailScheduler.ClearTrackingForLayoutKey(layoutKey);
        }

        private static int GetThumbnailQueueMaxParallelism() => ThumbnailQueueScheduler.GetMaxParallelism();


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
            if (e.TextComposition.CompositionText.Length == 0)
            {
                _imeFlag = false;
                if (_isApplyingSearchKeyword
                    || _isDeletingSearchHistory
                    || string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                {
                    return;
                }

                string text = SearchBox.Text ?? "";
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                // IME 確定時は TextChanged が来ないことがあるため、ここでも入力由来として印を付ける。
                _pendingTypedSearchHistory = true;
                if (SearchInputClassifier.IsIncrementalSearchEligible(text))
                {
                    ScheduleIncrementalSearch(text);
                }
            }
        }

        // 今後の検討メモ:
        // - AND 以外の検索（最低限 NOT）
        // - 検索履歴の保管条件（ヒット件数ベースか等）の見直し
        // - プロパティ表示ウィンドウの追加
        // - 重複チェック条件の整理（movie_name / Hash / 大文字小文字差異）

        private void OpenDatafile(string dbFullPath)
        {
            _ = OpenDatafileAsync(dbFullPath);
        }

        private async Task OpenDatafileAsync(string dbFullPath)
        {
            _openingDatabase = true;
            try
            {
                // DB 切替中は一覧更新を抑止する（_openingDatabase）。
                CancelActiveThumbnailWork();
                _fileWatcherManager.Clear();
                _discoveredFileRegistrationGate.Clear();
                ClearPendingDiscoveredThumbnailWork();
                watchData?.Clear();
                MainVM.DbInfo.SearchKeyword = "";
                _pendingTypedSearchHistory = false;
                _lastThumbnailScopeSearchKeyword = "";
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

                    SkinEngine startupEngine = PrepareStartupSkinMode(MainVM.DbInfo.Skin);
                    string sortId = MainVM.DbInfo.Sort ?? "1";
                    await FilterAndSortAsync(sortId, true, startupEngine).ConfigureAwait(true);

                    GetBookmarkTable(session);
                    GetTagBarTable(session);
                }

                SetSkinViewRoots();

                // DB オープン中は SwitchSkinEngine が早期 return するため、ここでサムネ生成を起動する。
                StartTabSwitchThumbnailJob(ThumbnailLayoutResolver.GetActiveListLayout(_currentSkinEngine));

                // DB オープン/切替直後は何も選択されておらず Avalon 詳細ペインが空になる。
                // 現在の表示モード（WPF/WB）とドロップダウン状態のまま先頭レコードを選択し、
                // 詳細を表示しておく。WB の遅延描画（ContextIdle で Tag 設定）後に走るよう、
                // 同優先度でキューの後ろに積む。
                _ = Dispatcher.BeginInvoke(
                    new Action(() => TabSelectionHelper.SelectFirstItem(this)),
                    DispatcherPriority.ContextIdle);

                CreateWatcher();
                ScheduleStartupFolderCheck();
                RefreshDmmPendingMenuBadge();
            }
            finally
            {
                _openingDatabase = false;
            }
        }

        private void SetLoadingOverlayMessage(string message)
        {
            if (LoadingOverlayMessage != null)
            {
                LoadingOverlayMessage.Text = message;
            }
        }

        private void PushLoadingOverlay(string message, bool cancelPendingSearchOverlay = true)
        {
            if (cancelPendingSearchOverlay)
            {
                CancelPendingSearchOverlay();
            }

            SetLoadingOverlayMessage(message);
            _loadingOverlayDepth++;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void PopLoadingOverlay()
        {
            _loadingOverlayDepth = Math.Max(0, _loadingOverlayDepth - 1);
            if (_loadingOverlayDepth == 0)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelPendingSearchOverlay()
        {
            if (_searchOverlayDelayCts == null)
            {
                return;
            }

            _searchOverlayDelayCts.Cancel();
            _searchOverlayDelayCts.Dispose();
            _searchOverlayDelayCts = null;
        }

        private void BeginSearchOverlayDelayed()
        {
            CancelPendingSearchOverlay();
            _searchOverlayPushed = false;
            var cts = new CancellationTokenSource();
            _searchOverlayDelayCts = cts;
            _ = RunSearchOverlayDelayAsync(cts);
        }

        private async Task RunSearchOverlayDelayAsync(CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(SearchOverlayDelayMs, cts.Token).ConfigureAwait(true);
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (cts.IsCancellationRequested || !ReferenceEquals(_searchOverlayDelayCts, cts))
                    {
                        return;
                    }

                    PushLoadingOverlay("検索中...", cancelPendingSearchOverlay: false);
                    _searchOverlayPushed = true;
                });
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void EndSearchOverlayDelayed()
        {
            CancelPendingSearchOverlay();
            if (_searchOverlayPushed)
            {
                _searchOverlayPushed = false;
                PopLoadingOverlay();
            }
        }

        private SkinEngine GetDefaultResolveEngine() =>
            _currentSkinEngine != default
                ? _currentSkinEngine
                : MainVM.DbInfo.CurrentSkinEngine;

        private async Task ReloadMovieRecordsAsync(string sortId, SkinEngine? resolveEngineOnly = null)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            PushLoadingOverlay("読み込み中...");
            try
            {
                _sessionState.BumpFilterGeneration();
                SkinEngine engine = resolveEngineOnly ?? GetDefaultResolveEngine();
                MovieListCoordinator.ReloadResult loaded = await _movieListCoordinator.ReloadAsync(
                    MainVM.DbInfo.DBFullPath,
                    sortId,
                    _thumbLayoutCache,
                    engine).ConfigureAwait(true);

                MovieListCoordinator.ReplaceCollection(MainVM.MovieRecs, loaded.Records);
                _movieRecordsLoaded = true;
                InvalidateAllItemsFilterCache();
            }
            finally
            {
                PopLoadingOverlay();
            }
        }

        private void RefreshThumbPathCache()
        {
            _thumbLayoutCache.Refresh(
                MainVM.DbInfo.DBName,
                MainVM.DbInfo.ThumbFolder);
        }

        private void SetSkinViewRoots()
        {
            string thumbRoot = ApplicationPaths.ResolveThumbRoot(MainVM.DbInfo.DBName, MainVM.DbInfo.ThumbFolder);
            string imagesRoot = ApplicationPaths.ImagesDirectory;
            SkinViewGridWb.UpdateHostMappings(thumbRoot, imagesRoot);
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
            EnsureMissingBookmarkThumbnails();
        }

        private void GetTagBarTable(SQLiteSession session = null)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                MainVM.TagBarRecs.Clear();
                return;
            }

            EnsureTagBarTable(MainVM.DbInfo.DBFullPath);
            EnsureBuiltInStarRatingItems(MainVM.DbInfo.DBFullPath);
            tagBarData = QueryDb(MainVM.DbInfo.DBFullPath, TagBarService.SelectAllOrderedSql, session);
            TagBarService.LoadInto(tagBarData, MainVM.TagBarRecs);
        }

        private void EnsureMissingBookmarkThumbnails()
        {
            foreach (MovieRecords bookmark in MainVM.BookmarkRecs)
            {
                if (!BookmarkThumbnailRestoreService.TryPrepareRestore(
                        bookmark,
                        MainVM.MovieRecs,
                        out string sourceMoviePath,
                        out string saveThumbPath,
                        out int capturePosSeconds))
                {
                    continue;
                }

                if (!_bookmarkThumbInFlight.Add(saveThumbPath))
                {
                    continue;
                }

                _ = CreateBookmarkThumbAsync(sourceMoviePath, saveThumbPath, capturePosSeconds);
            }
        }

        public void RequestDetailThumbnailRecreate()
        {
            if (viewExtDetail.DataContext is not MovieRecords mv)
            {
                return;
            }

            EnsureDetailThumbnail(mv, forceRecreate: true);
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

        private void PromoteSearchHistory(string keyword)
        {
            HistoryService.PromoteSearchHistory(MainVM.HistoryRecs, keyword);
        }

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
                WatchFolderDmmAutoService.EnsureSchema(dbPath);
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
            if (_currentSkinEngine == SkinEngine.Wb)
            {
                return;
            }

            string skinName = GetCurrentWpfSkinFolder();
            if (!string.IsNullOrWhiteSpace(skinName))
            {
                UpsertSystemTable(Properties.Settings.Default.LastDoc, "skin", skinName);
            }
        }

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

        private void SetActiveListItemsSource(IEnumerable<MovieRecords> items)
        {
            switch (_currentSkinEngine)
            {
                case SkinEngine.Wpf:
                    ResolveThumbPathsForActiveEngine(items);
                    WpfSkinList.ItemsSource = items;
                    break;
                case SkinEngine.Wb:
                    RenderSkinViewForCurrentFilter(deferUntilVisible: true);
                    break;
            }
        }

        private async Task RenameThumb(string eFullPath, string oldFullPath)
        {
            try
            {
                foreach (var item in MainVM.MovieRecs.Where(x => x.Movie_Path == oldFullPath))
                {
                    item.Movie_Path = eFullPath;
                    item.Movie_Name = Path.GetFileName(eFullPath);
                    item.Movie_Body = Path.GetFileNameWithoutExtension(eFullPath);
                    string dbMovieName = Path.GetFileNameWithoutExtension(eFullPath).ToLowerInvariant();

                    //DB内のデータ更新＆サムネイルのファイル名変更処理
                    UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, item.Movie_Id, "movie_path", item.Movie_Path);
                    UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, item.Movie_Id, "movie_name", dbMovieName);

                    //サムネイルのリネーム
                    var checkFileName = Path.GetFileNameWithoutExtension(oldFullPath);
                    MovieRenameService.RenameThumbnailFiles(
                        MainVM.DbInfo.ThumbFolder,
                        MainVM.DbInfo.DBName,
                        checkFileName,
                        dbMovieName,
                        item.Hash,
                        item);

                    if (Path.Exists(BookmarkRecordMapper.ResolveBookmarkFolder(MainVM.DbInfo.BookmarkFolder, MainVM.DbInfo.DBName)))
                    {
                        MovieRenameService.RenameBookmarkFiles(
                            MainVM.DbInfo.BookmarkFolder,
                            MainVM.DbInfo.DBName,
                            checkFileName,
                            dbMovieName);
                        UpdateBookmarkRename(MainVM.DbInfo.DBFullPath, checkFileName, dbMovieName);
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

        private async Task FilterAndSortAsync(
            string id,
            bool isGetNew = false,
            SkinEngine? resolveEngineOnly = null,
            bool forceThumbnailRestart = false,
            bool updateThumbnailScope = true)
        {
            if (!_movieRecordsLoaded || isGetNew)
            {
                await ReloadMovieRecordsAsync(id, resolveEngineOnly).ConfigureAwait(true);
            }

            await ApplyFilterAndSortAsync(id, forceThumbnailRestart, updateThumbnailScope).ConfigureAwait(true);
        }

        private async Task ApplyFilterAndSortAsync(
            string id,
            bool forceThumbnailRestart = false,
            bool updateThumbnailScope = true)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
            bool cacheHit = false;
#endif
            int capturedGeneration = _sessionState.FilterGeneration;
            string searchKeyword = MainVM.DbInfo.SearchKeyword ?? "";
            bool showAll = string.IsNullOrEmpty(searchKeyword);

            MovieListCoordinator.FilterApplyResult result;
            if (showAll && TryGetCachedAllItemsFilter(id, out MovieListCoordinator.FilterApplyResult cachedResult))
            {
                result = cachedResult;
#if DEBUG
                cacheHit = true;
#endif
            }
            else
            {
                BeginSearchOverlayDelayed();
                try
                {
                    List<MovieRecords> snapshot = [.. MainVM.MovieRecs];
                    SkinEngine currentEngine = _currentSkinEngine != default
                        ? _currentSkinEngine
                        : MainVM.DbInfo.CurrentSkinEngine;
                    var filterContext = new MovieListFilterContext
                    {
                        CurrentSkinEngine = currentEngine,
                        ThumbnailCache = _thumbLayoutCache,
                        DbFullPath = MainVM.DbInfo.DBFullPath,
                    };
                    result = await Task.Run(() =>
                        MovieListCoordinator.ApplyFilter(snapshot, searchKeyword, id, filterContext)).ConfigureAwait(true);

                    if (showAll)
                    {
                        StoreAllItemsFilterCache(id, result.Items);
                    }
                }
                finally
                {
                    EndSearchOverlayDelayed();
                }
            }

            if (capturedGeneration != _sessionState.FilterGeneration)
            {
                return;
            }

            if (!string.Equals(searchKeyword, MainVM.DbInfo.SearchKeyword ?? "", StringComparison.Ordinal))
            {
                return;
            }

            filterList = result.Items;
            MainVM.DbInfo.SearchCount = result.SearchCount;

            viewExtDetail.Visibility = MainVM.DbInfo.SearchCount == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            SetActiveListItemsSource(filterList);
            if (!showAll)
            {
                Refresh();
            }

            if (!_openingDatabase && IsMovieListActive)
            {
                string currentKeyword = MainVM.DbInfo.SearchKeyword ?? "";
                bool keywordChanged = !string.Equals(
                    _lastThumbnailScopeSearchKeyword,
                    currentKeyword,
                    StringComparison.Ordinal);

                if (forceThumbnailRestart)
                {
                    _lastThumbnailScopeSearchKeyword = currentKeyword;
                    RestartThumbnailsForActiveFilter(
                        useFullLibrary: ShouldUseFullLibraryForThumbnailRestart());
                }
                else if (updateThumbnailScope && keywordChanged)
                {
                    _lastThumbnailScopeSearchKeyword = currentKeyword;
                    RestartThumbnailsForActiveFilter(useFullLibrary: showAll);
                }
            }
#if DEBUG
            sw.Stop();
            Debug.WriteLine($"絞り込み経過時間 FilterAndSort：{sw.ElapsedMilliseconds} ミリ秒 (showAll={showAll}, cacheHit={cacheHit})");
#endif
        }

        private void CancelIncrementalSearchDebounce()
        {
            if (_searchIncrementalDebounceCts == null)
            {
                return;
            }

            _searchIncrementalDebounceCts.Cancel();
            _searchIncrementalDebounceCts.Dispose();
            _searchIncrementalDebounceCts = null;
        }

        private void ScheduleIncrementalSearch(string text)
        {
            CancelIncrementalSearchDebounce();
            var cts = new CancellationTokenSource();
            _searchIncrementalDebounceCts = cts;
            _ = RunIncrementalSearchDebouncedAsync(text, cts);
        }

        private async Task RunIncrementalSearchDebouncedAsync(string text, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(SearchIncrementalDebounceMs, cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cts != _searchIncrementalDebounceCts)
            {
                return;
            }

            _searchIncrementalDebounceCts = null;
            cts.Dispose();

            if (_isApplyingSearchKeyword || _imeFlag)
            {
                return;
            }

            if (!string.Equals(SearchBox.Text, text, StringComparison.Ordinal))
            {
                return;
            }

            if (!SearchInputClassifier.IsIncrementalSearchEligible(text))
            {
                return;
            }

            _sessionState.BumpFilterGeneration();
            await FilterAndSortAsync(MainVM.DbInfo.Sort, updateThumbnailScope: false).ConfigureAwait(true);
            SelectFirstItem();
        }

        private void DataRowToViewData(DataRow row, SkinEngine? resolveEngineOnly = null)
        {
            MainVM.MovieRecs.Add(
                MovieRecordMapper.FromDataRow(row, _thumbLayoutCache, resolveEngineOnly)
            );
        }

        private void ResolveThumbPathsForActiveEngine(IEnumerable<MovieRecords> records = null) =>
            ThumbPathHelper.ResolveThumbPathsForEngine(
                records ?? filterList,
                _thumbLayoutCache,
                _currentSkinEngine);

        private void RenderSkinViewForCurrentFilter(bool deferUntilVisible)
        {
            UserControls.SkinView skinView = TabSelectionHelper.GetSkinView(this);
            if (skinView == null)
            {
                return;
            }

            void Render()
            {
                IEnumerable<MovieRecords> items = filterList ?? [];
                ResolveThumbPathsForActiveEngine(items);
                skinView.Tag = items;
                skinView.RenderItems(items);
                skinView.FocusContent();
            }

            if (deferUntilVisible)
            {
                Dispatcher.BeginInvoke(Render, DispatcherPriority.ContextIdle);
                return;
            }

            Render();
        }

        private void TagCopy_Click(object sender, RoutedEventArgs e)
        {
            MovieRecords mv = GetSelectedMovie();
            if (mv == null) { return; }

            if (mv.Tags == null) { return; }
            if (mv.Tags.Length == 0) { return; }

            _ = ClipboardAccess.TrySetText(mv.Tags);
        }

        private void TagPaste_Click(object sender, RoutedEventArgs e)
        {
            if (!ClipboardAccess.TryGetText(out string clipboardText) || string.IsNullOrEmpty(clipboardText))
            {
                return;
            }

            List<MovieRecords> mv;
            mv = GetSelectedMovies();
            if (mv == null) { return; }

            foreach (var rec in mv)
            {
                TagMutationService.ApplyPaste(rec, clipboardText);
                UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, rec.Movie_Id, "tag", rec.Tags);
            }

            Refresh();
        }

        private void TagAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!IsMovieListActive) { return; }

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
            mv = GetSelectedMovies();
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
            if (!IsMovieListActive) { return; }

            MovieRecords mvSelected = GetSelectedMovie();
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
            mv = GetSelectedMovies();
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
            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
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

        private void MetadataEdit_Click(object sender, RoutedEventArgs e) => OpenMetadataEdit();

        private void MetadataCopy_Click(object sender, RoutedEventArgs e)
        {
            MovieRecords mv = GetSelectedMovie();
            if (mv == null)
            {
                return;
            }

            _ = ClipboardAccess.TrySetText(MetadataClipboard.Serialize(MetadataEditModel.FromMovie(mv)));
        }

        private void MetadataPaste_Click(object sender, RoutedEventArgs e)
        {
            if (!ClipboardAccess.TryGetText(out string clipboardText) ||
                !MetadataClipboard.TryDeserialize(clipboardText, out MetadataEditModel model))
            {
                return;
            }

            List<MovieRecords> mv = GetSelectedMovies();
            if (mv == null)
            {
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            foreach (MovieRecords rec in mv)
            {
                model.ApplyTo(rec);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Title, rec.Title);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Comment1, rec.Comment1);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Comment2, rec.Comment2);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Comment3, rec.Comment3);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Artist, rec.Artist);
                UpdateMovieSingleColumn(dbPath, rec.Movie_Id, MovieColumn.Genre, rec.Genre);
            }

            Refresh();
            viewExtDetail.Refresh();
        }

        private void OpenMetadataEdit()
        {
            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
            if (mv == null) { return; }

            var editModel = MetadataEditModel.FromMovie(mv);
            var window = new MetadataEditWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = editModel,
            };
            window.ShowDialog();

            if (window.CloseStatus() != MessageBoxResult.OK)
            {
                return;
            }

            editModel.ApplyTo(mv);

            string dbPath = MainVM.DbInfo.DBFullPath;
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Title, mv.Title);
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Comment1, mv.Comment1);
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Comment2, mv.Comment2);
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Comment3, mv.Comment3);
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Artist, mv.Artist);
            UpdateMovieSingleColumn(dbPath, mv.Movie_Id, MovieColumn.Genre, mv.Genre);

            Refresh();
            viewExtDetail.Refresh();
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
                if (!IsMovieListActive) { return; }

                List<MovieRecords> mv;
                mv = GetSelectedMovies();
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

            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
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
            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
            if (mv == null) { return; }

            if (Path.Exists(mv.Movie_Path))
            {
                if (Path.Exists(mv.Dir))
                {
                    Process.Start("explorer.exe", $"/select,{mv.Movie_Path}");
                }
            }
        }

        private static void OpenWpfSkinPathLink(MovieRecords mv, string field)
        {
            if (mv == null)
            {
                return;
            }

            string key = field?.Trim().ToLowerInvariant() ?? "path";
            try
            {
                string textValue = key switch
                {
                    "dir" => mv.Dir,
                    "drive" => mv.Drive,
                    "comment1" => mv.Comment1,
                    "comment2" => mv.Comment2,
                    "comment3" => mv.Comment3,
                    "path" => mv.Movie_Path,
                    _ => mv.Movie_Path,
                };

                if (TryOpenAsUrl(textValue))
                {
                    return;
                }

                if (key is "dir")
                {
                    if (!string.IsNullOrWhiteSpace(mv.Dir) && Path.Exists(mv.Dir))
                    {
                        Process.Start("explorer.exe", mv.Dir);
                    }

                    return;
                }

                if (key is "drive")
                {
                    string drive = mv.Drive;
                    if (string.IsNullOrWhiteSpace(drive))
                    {
                        return;
                    }

                    if (!drive.EndsWith('\\') && !drive.EndsWith('/'))
                    {
                        drive += Path.DirectorySeparatorChar;
                    }

                    if (Path.Exists(drive))
                    {
                        Process.Start("explorer.exe", drive);
                    }

                    return;
                }

                if (!string.IsNullOrWhiteSpace(mv.Movie_Path) && Path.Exists(mv.Movie_Path))
                {
                    Process.Start("explorer.exe", $"/select,{mv.Movie_Path}");
                }
            }
            catch
            {
                // リンク失敗は無視（存在しないパス等）
            }
        }

        private static bool TryOpenAsUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
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

            if (!IsMovieListActive) { return; }
            MovieRecords mv = GetSelectedMovie();
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
            var destMoveFile = mv.Movie_Path.Replace(checkFileName, newFilePath, StringComparison.CurrentCultureIgnoreCase);
            var destFolder = Path.GetDirectoryName(destMoveFile);
            destMoveFile = destMoveFile.Replace(checkExt, newExt, StringComparison.OrdinalIgnoreCase);
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
            _ = RenameThumb(destMoveFile, mv.Movie_Path);

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
            string keyName = ResolveDeleteKeyName(sender, e);

            if (!(keyName.ToLower() is "delete" or "deletemovie" or "deletefile"))
            {
                return;
            }

            if (!IsMovieListActive) { return; }

            List<MovieRecords> mv;
            mv = GetSelectedMovies();
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
                radio1Content = "ゴミ箱に移動して削除(_G)";
                radio2Content = "ディスクから完全に削除(_D)";
            }

            var dialogWindow = new MessageBoxEx(this)
            {
                CheckBoxContent = "サムネイルも削除する(_S)",
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
                DmmPendingCandidateStore.DeleteByMovieId(MainVM.DbInfo.DBFullPath, rec.Movie_Id);

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
            RefreshDmmPendingMenuBadge();
        }

        private static string ResolveDeleteKeyName(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                return menuItem.Name ?? string.Empty;
            }

            if (e is KeyEventArgs)
            {
                // Shift+Delete → ファイル削除ダイアログ。Delete 単体は登録削除のまま。
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    return "DeleteFile";
                }

                return "Delete";
            }

            return string.Empty;
        }

        private static string AppTitle =>
            Assembly.GetExecutingAssembly().GetName().Name;

        private bool EnsureDatabaseSelected()
        {
            if (!string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return true;
            }

            MessageBox.Show(
                "管理ファイルが選択されていません。",
                AppTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Exclamation);
            return false;
        }

        private void BtnReCreateThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDatabaseSelected())
            {
                return;
            }

            if (!IsMovieListActive) { return; }

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
            List<QueueObj> thumbQueue = [.. MainVM.MovieRecs.Select(item =>
            {
                var queueItem = new QueueObj
                {
                    MovieId = item.Movie_Id,
                    MovieFullPath = item.Movie_Path,
                };
                PopulateActiveListQueueLayout(queueItem);
                return queueItem;
            })];
            EnqueueThumbnailWork(thumbQueue, beginNewJob: true);
        }

        private void BeginRefreshAllFileInfoFromMenu()
        {
            if (!EnsureDatabaseSelected())
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
            if (!SinkuMetadataFetcher.IsAvailable
                || string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            List<MovieRecords> targets = GetSelectedMovies();
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
            CountedProgressSession session = null;

            try
            {
                session = await Dispatcher.InvokeAsync(() => CountedProgressSession.BeginFileInfo(targets.Count));
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

        /// <summary>
        /// 新規登録直後に sinku が空振りした場合の補完。既に video/container がある行は触らない。
        /// </summary>
        private void EnqueueSinkuRefreshForDiscovered(IReadOnlyList<QueueObj> items)
        {
            if (!SinkuMetadataFetcher.IsAvailable || items == null || items.Count == 0)
            {
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            if (string.IsNullOrEmpty(dbPath))
            {
                return;
            }

            List<(long MovieId, string MoviePath)> targets = items
                .Where(item => item != null
                    && item.MovieId > 0
                    && !string.IsNullOrWhiteSpace(item.MovieFullPath)
                    && !ZipMediaKind.IsZipPath(item.MovieFullPath))
                .Select(item => (item.MovieId, item.MovieFullPath))
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                foreach ((long movieId, string moviePath) in targets)
                {
                    if (!_sessionState.IsActiveDb(dbPath))
                    {
                        return;
                    }

                    MovieRecords rec = null;
                    RunOnUi(() =>
                    {
                        rec = MainVM.MovieRecs?.FirstOrDefault(r => r.Movie_Id == movieId);
                    });

                    if (rec == null)
                    {
                        rec = new MovieRecords
                        {
                            Movie_Id = movieId,
                            Movie_Path = moviePath,
                        };
                    }
                    else if (!string.IsNullOrWhiteSpace(rec.Video)
                        && !string.IsNullOrWhiteSpace(rec.Container))
                    {
                        continue;
                    }

                    try
                    {
                        RefreshFileInfoCore(dbPath, rec);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [sinku-refresh] {moviePath} : {ex.Message}");
                    }
                }
            });
        }

        private void RequestApplicationExit() => Close();


        private void OperationStatusCancel_Click(object sender, RoutedEventArgs e)
        {
            _statusBarProgress.RequestCancelActive();
        }

        private void CreateNewDatabase()
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
                AppSettingsPersistence.SaveLastDoc(sfd.FileName);
            }
        }

        private void ReStackRecentTree(string newItem)
        {
            recentFiles = RecentFilesService.ReStack(
                recentFiles,
                newItem,
                Properties.Settings.Default.RecentFilesCount);
            SyncRecentFilesUi();
        }

        private void SyncRecentFilesUi()
        {
            RecentFilesService.RebuildRecentItems(MainVM.RecentFileItems, recentFiles);
            JumpListService.SyncRecentFiles(recentFiles);
        }

        private void PersistRecentFilesToSettings() =>
            AppSettingsPersistence.SaveRecentFiles(recentFiles.Reverse());

        /// <summary>
        /// 起動引数の .wb を開く。成功したら true（LastDoc 自動オープンより優先）。
        /// </summary>
        private bool TryOpenStartupDocument()
        {
            string path = App.StartupDocumentPath;
            if (string.IsNullOrWhiteSpace(path) || !Path.Exists(path))
            {
                return false;
            }

            ReStackRecentTree(path);
            PersistRecentFilesToSettings();
            AppSettingsPersistence.SaveLastDoc(path);
            _ = OpenDatafileAsync(path);
            return true;
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
            SyncRecentFilesUi();
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

            ExecuteNavigation(item.Id);

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

        private void ExecuteNavigation(string id)
        {
            switch (id)
            {
                case NavigationActionIds.New:
                    CreateNewDatabase();
                    break;
                case NavigationActionIds.Open:
                    OpenDatabaseFile();
                    break;
                case NavigationActionIds.Exit:
                    RequestApplicationExit();
                    break;
                case NavigationMenuIds.CommonSettings:
                case NavigationMenuIds.DatabaseSettings:
                    ExecuteSettingsNavigation(id);
                    break;
                case NavigationMenuIds.WatchFolderEdit:
                case NavigationMenuIds.WatchFolderCheck:
                case NavigationMenuIds.RecreateAllThumbnails:
                case NavigationMenuIds.RefreshAllFileInfo:
                case NavigationMenuIds.DmmBulkFetch:
                case NavigationMenuIds.DmmPendingCandidates:
                    ExecuteToolNavigation(id);
                    break;
                case NavigationMenuIds.DmmTagExcludeList:
                    ExecuteDmmTagExcludeNavigation();
                    break;
                case NavigationMenuIds.WpfSkinEdit:
                case NavigationMenuIds.WpfSkinNew:
                case NavigationMenuIds.WpfSkinDelete:
                    ExecuteWpfSkinMaintenanceNavigation(id);
                    break;
                default:
                    OpenRecentFile(id);
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
            AppSettingsPersistence.SaveLastDoc(path);
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
                    if (!EnsureDatabaseSelected())
                    {
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

                    settingsWindow.CommitPreGenThumbSelection();
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
                    UpsertSystemTable(
                        MainVM.DbInfo.DBFullPath,
                        PreGenThumbSkinSelection.SystemAttrEnabled,
                        PreGenThumbSkinSelection.FormatEnabled(
                            settingsWindow.DataContext is DatabaseSettings ds && ds.PreGenThumbsOnNewMovies));
                    UpsertSystemTable(
                        MainVM.DbInfo.DBFullPath,
                        PreGenThumbSkinSelection.SystemAttrSkinKeys,
                        settingsWindow.DataContext is DatabaseSettings dsKeys
                            ? dsKeys.PreGenThumbSkinKeys
                            : string.Empty);

                    GetSystemTable(MainVM.DbInfo.DBFullPath);
                    break;
            }
        }

        private void ExecuteToolNavigation(string menuId)
        {
            if (!EnsureDatabaseSelected())
            {
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
                    if (!IsMovieListActive) { return; }

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

                    List<QueueObj> thumbQueue = [.. MainVM.MovieRecs.Select(rec =>
                    {
                        var queueItem = new QueueObj
                        {
                            MovieId = rec.Movie_Id,
                            MovieFullPath = rec.Movie_Path,
                        };
                        PopulateActiveListQueueLayout(queueItem);
                        return queueItem;
                    })];
                    EnqueueThumbnailWork(thumbQueue, beginNewJob: true);
                    break;

                case NavigationMenuIds.RefreshAllFileInfo:
                    BeginRefreshAllFileInfoFromMenu();
                    break;

                case NavigationMenuIds.DmmBulkFetch:
                    BeginDmmBulkFetchFromMenu();
                    break;

                case NavigationMenuIds.DmmPendingCandidates:
                    BeginDmmPendingCandidatesFromMenu();
                    break;
            }
        }

        /// <summary>DMM タグ除外リスト。アプリ共通のため DB 未選択でも利用できる。</summary>
        private void ExecuteDmmTagExcludeNavigation()
        {
            MenuToggleButton.IsChecked = false;
            var window = new DmmTagExcludeWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }

        /// <summary>WPF スキンメンテ。DB 未選択でも利用できる。</summary>
        private void ExecuteWpfSkinMaintenanceNavigation(string menuId)
        {
            MenuToggleButton.IsChecked = false;
            switch (menuId)
            {
                case NavigationMenuIds.WpfSkinEdit:
                    OpenWpfSkinMaintenance(SkinMaintenanceWindow.OpenMode.EditExisting, ResolveWpfSkinEditTarget());
                    break;
                case NavigationMenuIds.WpfSkinNew:
                    BeginWpfSkinCreateFromTemplate();
                    break;
                case NavigationMenuIds.WpfSkinDelete:
                    BeginWpfSkinDeleteFromMenu();
                    break;
            }
        }

        private string ResolveWpfSkinEditTarget()
        {
            if (_currentSkinEngine == SkinEngine.Wpf && ComboSkin.SelectedItem is string selected
                && !string.IsNullOrWhiteSpace(selected))
            {
                return selected;
            }

            string last = Properties.Settings.Default.LastWpfSkinName;
            if (!string.IsNullOrWhiteSpace(last)
                && Services.WpfSkin.WpfSkinStorage.FolderExists(last))
            {
                return last;
            }

            return Services.WpfSkin.WpfSkinLoader.DefaultSkinName;
        }

        private void BeginWpfSkinCreateFromTemplate()
        {
            var pick = new WpfSkinTemplatePickWindow(this);
            if (pick.ShowDialog() != true)
            {
                return;
            }

            if (pick.SelectedStructTemplate != null)
            {
                // 構造テンプレから直接組み立て
                var def = WpfSkinStorage.CreateFromStructTemplate(pick.SelectedStructTemplate);
                OpenWpfSkinMaintenance(
                    SkinMaintenanceWindow.OpenMode.CreateNew,
                    folderName: null,
                    templateFolderName: null,
                    prebuiltDefinition: def);
            }
            else if (!string.IsNullOrWhiteSpace(pick.SelectedTemplateName))
            {
                OpenWpfSkinMaintenance(
                    SkinMaintenanceWindow.OpenMode.CreateNew,
                    folderName: null,
                    templateFolderName: pick.SelectedTemplateName);
            }
        }

        private void OpenWpfSkinMaintenance(
            SkinMaintenanceWindow.OpenMode mode,
            string folderName,
            string templateFolderName = null,
            WpfSkinDefinition prebuiltDefinition = null)
        {
            var window = new SkinMaintenanceWindow(
                this,
                mode,
                folderName,
                GetSelectedMovie(),
                templateFolderName,
                prebuiltDefinition);
            window.LiveApplyRequested = ApplySavedWpfSkinFromMaintenance;
            window.ShowDialog();
            if (window.SkinWasSaved || window.SkinWasDeleted)
            {
                RefreshWpfSkinListAfterMaintenance(window.ResultSkinFolderName, preferSelectSaved: window.SkinWasSaved);
            }
            else if (_currentSkinEngine == SkinEngine.Wpf)
            {
                // プレビューが HostContext を上書きするため、キャンセル時も本番側へ戻す。
                // Name（表示名）ではなくフォルダキーで再適用する（不一致だと CardLarge へ落ちる）。
                string skinName = ComboSkin.SelectedItem as string
                    ?? GetCurrentWpfSkinFolder()
                    ?? Properties.Settings.Default.LastWpfSkinName;
                ApplyWpfSkin(string.IsNullOrWhiteSpace(skinName) ? null : skinName);
                _ = OnSkinLayoutChangedAsync();
            }
        }

        private void ApplySavedWpfSkinFromMaintenance(string skinFolderName)
        {
            if (string.IsNullOrWhiteSpace(skinFolderName))
            {
                return;
            }

            RefreshWpfSkinListAfterMaintenance(skinFolderName, preferSelectSaved: true);
            if (_currentSkinEngine == SkinEngine.Wpf)
            {
                ApplyWpfSkin(skinFolderName);
                TryAdoptNearThumbnailLayout(skinFolderName);
                _ = OnSkinLayoutChangedAsync();
            }
        }

        /// <summary>
        /// 適用スキンのサムネサイズに 1px 差の既存フォルダがあるとき、再利用するか確認する。
        /// Yes なら skin.json の thumbnail 寸法を既存に合わせて保存し直し、再生成を避ける。
        /// </summary>
        private void TryAdoptNearThumbnailLayout(string skinFolderName)
        {
            if (_wpfSkin?.Thumbnail == null)
            {
                return;
            }

            ThumbnailLayoutSpec wanted = ThumbnailLayoutSpec.FromWpfSkinThumbnail(_wpfSkin.Thumbnail);
            string thumbRoot = _thumbLayoutCache.ThumbRootPath;
            if (string.IsNullOrWhiteSpace(thumbRoot))
            {
                return;
            }

            // 目標フォルダに既にファイルがあるなら何もしない
            string wantedDir = wanted.GetOutPath(_thumbLayoutCache.DbName, _thumbLayoutCache.ThumbFolder);
            if (Directory.Exists(wantedDir)
                && Directory.EnumerateFiles(wantedDir, "*.jpg").Any())
            {
                return;
            }

            ThumbnailLayoutSpec near = ThumbnailLayoutNearMatch.FindNearExistingWithFiles(thumbRoot, wanted);
            if (near == null)
            {
                return;
            }

            var confirm = new MessageBoxEx(this)
            {
                DlogTitle = "近いサイズのサムネイル",
                DlogMessage =
                    $"指定サイズ {wanted.Key} のサムネはまだありませんが、\n"
                    + $"近いサイズ {near.Key} が既にあります（差は幅/高さ 1px 以内）。\n\n"
                    + "既存サイズを採用して再作成を省略しますか？\n"
                    + "（スキンの thumbnail.width / height を既存に合わせます）",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.ImageArea,
            };
            confirm.ShowDialog();
            if (confirm.CloseStatus() != MessageBoxResult.OK)
            {
                return;
            }

            _wpfSkin.Thumbnail.Width = near.Width;
            _wpfSkin.Thumbnail.Height = near.Height;
            SyncThumbnailNodeWidthsToGeneration(_wpfSkin, near.Width);

            if (!Services.WpfSkin.WpfSkinStorage.TrySave(
                    _wpfSkin,
                    skinFolderName,
                    overwriteExisting: true,
                    out string error))
            {
                MessageBox.Show(
                    this,
                    "近いサイズの採用を保存できませんでした。\n" + error,
                    Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ApplyWpfSkin(skinFolderName);
        }

        private static void SyncThumbnailNodeWidthsToGeneration(WpfSkinDefinition def, int width)
        {
            void Walk(WpfSkinNode node)
            {
                if (node == null)
                {
                    return;
                }

                if (string.Equals(node.Type, "thumbnail", StringComparison.OrdinalIgnoreCase)
                    && node.Width.HasValue)
                {
                    node.Width = width;
                }

                if (node.Children == null)
                {
                    return;
                }

                foreach (WpfSkinNode child in node.Children)
                {
                    Walk(child);
                }
            }

            Walk(def?.Card?.Layout);
        }

        private void BeginWpfSkinDeleteFromMenu()
        {
            IReadOnlyList<string> deletable = Services.WpfSkin.WpfSkinStorage.EnumerateDeletableSkins();
            if (deletable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "削除できるユーザースキンがありません。\n（Default 系はアプリから削除できません）",
                    Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var pick = new WpfSkinPickWindow(
                this,
                "WPFスキンを削除",
                "ゴミ箱へ移動するスキンを選んでください。（Default 系は一覧に出ません）",
                deletable);
            if (pick.ShowDialog() != true || string.IsNullOrWhiteSpace(pick.SelectedSkinName))
            {
                return;
            }

            string name = pick.SelectedSkinName;
            var confirm = new MessageBoxEx(this)
            {
                DlogTitle = "スキンをゴミ箱へ",
                DlogMessage = $"スキン「{name}」をゴミ箱へ移動します。よろしいですか？",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.Delete,
            };
            confirm.ShowDialog();
            if (confirm.CloseStatus() != MessageBoxResult.OK)
            {
                return;
            }

            if (!Services.WpfSkin.WpfSkinStorage.TryDeleteToRecycleBin(name, out string error))
            {
                MessageBox.Show(this, error ?? "削除に失敗しました。", Assembly.GetExecutingAssembly().GetName().Name,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshWpfSkinListAfterMaintenance(name, preferSelectSaved: false);
        }

        private void RefreshWpfSkinListAfterMaintenance(string affectedName, bool preferSelectSaved)
        {
            bool wasWpf = _currentSkinEngine == SkinEngine.Wpf;
            string previous = GetCurrentWpfSkinFolder();
            RebuildSkinCombo(isWpf: true);

            if (!wasWpf)
            {
                return;
            }

            IReadOnlyList<string> skins = Services.WpfSkin.WpfSkinLoader.EnumerateSkins();
            string select = null;
            if (preferSelectSaved
                && !string.IsNullOrWhiteSpace(affectedName)
                && skins.Contains(affectedName))
            {
                select = affectedName;
            }
            else if (!string.IsNullOrWhiteSpace(previous) && skins.Contains(previous))
            {
                select = previous;
            }
            else if (skins.Contains(Services.WpfSkin.WpfSkinLoader.DefaultSkinName))
            {
                select = Services.WpfSkin.WpfSkinLoader.DefaultSkinName;
            }
            else
            {
                select = skins.FirstOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(select))
            {
                _suppressSkinComboChange = true;
                ComboSkin.SelectedItem = select;
                _suppressSkinComboChange = false;
                ApplyWpfSkin(select);
                Properties.Settings.Default.LastWpfSkinName = select;
                Properties.Settings.Default.Save();
                _ = OnSkinLayoutChangedAsync();
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

        private void OpenDatabaseFile()
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
                AppSettingsPersistence.SaveLastDoc(ofd.FileName);
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
            // DB 未オープン時は SQLite クエリが "Data Source cannot be empty" で
            // 落ちるため、何もせず抜ける（スキンの Reload と同じく無反応にする）。
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            // フォルダの最新状態をDBに反映
            //await CheckFolderAsync(CheckMode.Auto);

            // ブックマーク・リスト等の再取得
            GetBookmarkTable();
            BookmarkList.Items.Refresh();
            GetTagBarTable();
            TagBarList.Items.Refresh();
            await FilterAndSortAsync(MainVM.DbInfo.Sort, isGetNew: true, forceThumbnailRestart: true).ConfigureAwait(true);
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
                if (!IsMovieListActive) { return; }

                mv = GetSelectedMovie();
                if (mv == null) { return; }

                moviePath = $"\"{mv.Movie_Path}\"";

                if (!Path.Exists(mv.Movie_Path))
                {
                    return;
                }

                if (sender is MenuItem senderObj && senderObj.Name == "PlayFromThumb")
                {
                    if (!TryResolvePlayPositionFromThumb(mv, _currentSkinEngine, out _, out msec))
                    {
                        msec = GetPlayPosition(_currentSkinEngine, mv, ref secPos);
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.E
                && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
            {
                FocusSearchBox();
                e.Handled = true;
            }
        }

        private void FocusSearchBox()
        {
            if (SearchBox == null)
            {
                return;
            }

            // ドロワーが開いていれば閉じずとも検索は触れるが、フォーカスを確実に移す
            _ = SearchBox.Focus();
            if (SearchBox.Template?.FindName("PART_EditableTextBox", SearchBox) is TextBox editable)
            {
                _ = editable.Focus();
                editable.SelectAll();
            }
        }

        private async void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_isApplyingSearchKeyword) { return; }

            if (!IsMovieListActive) { return; }

            MovieRecords mv = GetSelectedMovie();
            if (mv == null) { return; }

            string keyword = MainVM.DbInfo.SearchKeyword;
            if (string.IsNullOrEmpty(keyword))
            {
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            // インクリメンタル入力の確定は LostFocus。文字ごとの履歴化はしない。
            // TagBar 等（入力由来でない）は _pendingTypedSearchHistory が立っていないので書かない。
            bool recordHistory = _pendingTypedSearchHistory;
            if (recordHistory)
            {
                _pendingTypedSearchHistory = false;
                PromoteSearchHistory(keyword);
            }

            // LostFocus での同期DBアクセスがUI停止を起こしやすいのでバックグラウンド化。
            await Task.Run(() =>
            {
                InsertFindFactTable(dbPath, keyword);
                if (recordHistory)
                {
                    InsertHistoryTable(dbPath, keyword);
                }
            }).ConfigureAwait(true);
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_imeFlag) { return; }
            if (_isDeletingSearchHistory) { return; }
            if (_isApplyingSearchKeyword) { return; }

            // 手動でテキスト編集したらキーボードカーソルはリセットする。
            _historyCursor = -1;

            string text = SearchBox.Text ?? "";
            if (string.IsNullOrEmpty(text))
            {
                _pendingTypedSearchHistory = false;
                CancelIncrementalSearchDebounce();
                MainVM.DbInfo.SearchKeyword = "";
                _sessionState.BumpFilterGeneration();
                await FilterAndSortAsync(MainVM.DbInfo.Sort, updateThumbnailScope: true).ConfigureAwait(true);
                SelectFirstItem();
                return;
            }

            // ユーザー入力由来。LostFocus で履歴化する（debounce ごとには書かない）。
            _pendingTypedSearchHistory = true;

            if (!SearchInputClassifier.IsIncrementalSearchEligible(text))
            {
                CancelIncrementalSearchDebounce();
                return;
            }

            ScheduleIncrementalSearch(text);
        }

        private void SearchBox_DropDownClosed(object sender, EventArgs e)
        {
            _historyCursor = -1;
        }

        // MaterialDesign の ComboBox 既定テンプレート内のドロップダウン矢印（名前付き Path "arrow"）が
        // 右端に寄りすぎるため、右側に少し余白を足す。テンプレート外からは直接指定できないため、
        // Loaded 後に視覚ツリーから拾ってマージンを上書きする（右=4px 固定で多重適用しても安全）。
        private void ComboBoxArrow_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox combo)
            {
                return;
            }

            if (FindDescendantByName(combo, "arrow") is FrameworkElement arrow)
            {
                Thickness m = arrow.Margin;
                arrow.Margin = new Thickness(m.Left, m.Top, 4, m.Bottom);
            }
        }

        private static FrameworkElement FindDescendantByName(DependencyObject root, string name)
        {
            if (root == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is FrameworkElement fe && fe.Name == name)
                {
                    return fe;
                }

                FrameworkElement nested = FindDescendantByName(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void SearchBox_DropDownOpened(object sender, EventArgs e)
        {
            _historyCursor = -1;

            // 開いた直後はフォーカスがポップアップのコンテナ（項目以外）にあり、矢印・Enter が
            // 本体側・項目側どちらの PreviewKeyDown にも届かない（最初のキーが WPF 既定動作になる）。
            // 先頭項目へフォーカスを移して、最初から項目ハンドラでキーを拾えるようにする。
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (SearchBox.IsDropDownOpen && SearchBox.Items.Count > 0)
                    {
                        FocusHistoryContainer(SearchBox, 0);
                    }
                }),
                DispatcherPriority.Input);
        }

        // 矢印キーでドロップダウン内の選択を移動し、検索ボックスのテキストを候補に更新する（即時検索はしない）。
        private void MoveSearchHistoryHighlight(ComboBox combo, int direction)
        {
            int count = combo.Items.Count;
            if (count == 0)
            {
                return;
            }

            // 独自カーソルを基準に移動する（SelectedIndex は Text 設定で -1 に戻されるため使わない）。
            int current = _historyCursor;
            if (current < 0 || current >= count)
            {
                current = -1;
            }

            int next = current + direction;
            if (next < 0)
            {
                next = count - 1;
            }
            else if (next >= count)
            {
                next = 0;
            }

            if (combo.Items[next] is not History target)
            {
                return;
            }

            _historyCursor = next;
            string text = target.Find_Text ?? "";
            _isApplyingSearchKeyword = true;
            try
            {
                MainVM.DbInfo.SearchKeyword = text;
                SearchBox.Text = text;
                // 見た目のカーソルは SelectedIndex を最後に設定して反映する（IsSelected トリガー）。
                combo.SelectedIndex = next;
                BringSearchHistoryItemIntoView(combo, next);
            }
            finally
            {
                _isApplyingSearchKeyword = false;
            }

            // カーソル項目へフォーカスを移し、次のキーも項目側 PreviewKeyDown で確実に拾えるようにする。
            if (combo.IsDropDownOpen)
            {
                FocusHistoryContainer(combo, next);
            }
        }

        // 指定インデックスの ComboBoxItem を表示・フォーカスする（仮想化は無効化済みのため必ず取得できる）。
        private static void FocusHistoryContainer(ComboBox combo, int index)
        {
            if (index < 0 || index >= combo.Items.Count)
            {
                return;
            }

            if (combo.ItemContainerGenerator.ContainerFromIndex(index) is ComboBoxItem container)
            {
                container.BringIntoView();
                container.Focus();
            }
        }

        // 指定インデックスの ComboBoxItem を表示範囲内へスクロールする。
        private static void BringSearchHistoryItemIntoView(ComboBox combo, int index)
        {
            if (index < 0 || index >= combo.Items.Count)
            {
                return;
            }

            if (combo.ItemContainerGenerator.ContainerFromIndex(index) is ComboBoxItem container)
            {
                container.BringIntoView();
            }
        }

        // 削除対象の履歴項目を返す。キーボードカーソル（_historyCursor）を優先し、無ければマウスホバー項目。
        private History GetActiveSearchHistory(ComboBox combo)
        {
            if (_historyCursor >= 0
                && _historyCursor < combo.Items.Count
                && combo.Items[_historyCursor] is History current)
            {
                return current;
            }

            foreach (object obj in combo.Items)
            {
                if (combo.ItemContainerGenerator.ContainerFromItem(obj) is ComboBoxItem { IsMouseOver: true } container
                    && container.DataContext is History hovered)
                {
                    return hovered;
                }
            }

            return combo.SelectedItem as History;
        }

        // ドロップダウンリスト内でマウスクリック時に検索
        private async void SearchBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSearchHistoryDeleteButtonSource(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (sender is not ComboBoxItem item)
            {
                return;
            }

            e.Handled = true;
            if (item.DataContext is History history)
            {
                SearchBox.IsDropDownOpen = false;
                await SearchByKeywordAsync(history.Find_Text ?? "").ConfigureAwait(true);
                return;
            }

            string keyword = item.Content?.ToString() ?? "";
            SearchBox.IsDropDownOpen = false;
            await SearchByKeywordAsync(keyword).ConfigureAwait(true);
        }

        private void SearchHistoryDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement element
                && element.DataContext is History history)
            {
                RemoveSearchHistoryItem(history);
            }
        }

        private void SearchHistoryDeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem
                || menuItem.Parent is not ContextMenu contextMenu
                || contextMenu.PlacementTarget is not FrameworkElement target)
            {
                return;
            }

            if (target.DataContext is History history)
            {
                RemoveSearchHistoryItem(history);
            }
        }

        private void RemoveSearchHistoryItem(History history)
        {
            if (history == null
                || string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            string keepText = SearchBox.Text ?? "";
            string findText = history.Find_Text;
            bool keepDropdownHighlight = SearchBox.IsDropDownOpen;
            int removedIndex = MainVM.HistoryRecs.IndexOf(history);

            // 削除位置と同じインデックス（末尾削除なら1つ上）を次のカーソル位置にする。
            int nextIndex = -1;
            if (keepDropdownHighlight && removedIndex >= 0 && MainVM.HistoryRecs.Count > 1)
            {
                nextIndex = Math.Min(removedIndex, MainVM.HistoryRecs.Count - 2);
            }

            _isDeletingSearchHistory = true;
            try
            {
                MainVM.HistoryRecs.Remove(history);

                // 入力テキストは消さずに残す。カーソルだけ次の項目へ。
                _historyCursor = nextIndex;
                SearchBox.SelectedIndex = nextIndex;
                SearchBox.Text = keepText;
                if (!string.Equals(MainVM.DbInfo.SearchKeyword, keepText, StringComparison.Ordinal))
                {
                    MainVM.DbInfo.SearchKeyword = keepText;
                }
            }
            finally
            {
                _isDeletingSearchHistory = false;
            }

            if (keepDropdownHighlight && nextIndex >= 0)
            {
                // 削除後はカーソル項目へフォーカスを移し、続けて矢印・Enter を項目側で拾えるようにする。
                SearchBox.Dispatcher.BeginInvoke(
                    () => FocusHistoryContainer(SearchBox, nextIndex),
                    DispatcherPriority.Loaded);
            }

            // Find_Id=0（今セッションで追加直後）でも DB 行を消せるよう find_text 基準。
            if (!string.IsNullOrEmpty(findText))
            {
                string dbPath = MainVM.DbInfo.DBFullPath;
                _ = Task.Run(() => DeleteHistoryTable(dbPath, findText));
            }

            _statusBarProgress.ShowIdleStatusMessage("検索履歴を削除しました");
        }

        private static bool IsSearchHistoryDeleteButtonSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button button && button.Content as string == "×")
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        // ComboBox 本体（編集テキストボックスにフォーカスがある場合）からのキー入力。
        private async void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox combo)
            {
                await HandleSearchBoxKeyAsync(combo, e).ConfigureAwait(true);
            }
        }

        // ドロップダウン項目（ポップアップ）にフォーカスがある場合のキー入力。
        // ドロップダウンを開くとフォーカスがポップアップ側へ移るため、項目側でもキーを拾う必要がある。
        private async void SearchBoxItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBoxItem item
                && ItemsControl.ItemsControlFromItemContainer(item) is ComboBox combo)
            {
                await HandleSearchBoxKeyAsync(combo, e).ConfigureAwait(true);
            }
        }

        private async Task HandleSearchBoxKeyAsync(ComboBox combo, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath)) { return; }
            if (_imeFlag) { return; }
            if (e.Handled) { return; }

            if (combo.IsDropDownOpen && (e.Key == Key.Down || e.Key == Key.Up))
            {
                e.Handled = true;
                MoveSearchHistoryHighlight(combo, e.Key == Key.Down ? 1 : -1);
                return;
            }

            if (combo.IsDropDownOpen
                && e.Key == Key.Delete
                && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                History deleteHistory = GetActiveSearchHistory(combo);
                if (deleteHistory != null)
                {
                    e.Handled = true;
                    RemoveSearchHistoryItem(deleteHistory);
                }

                return;
            }

            // Enter は常に入力テキストで検索する（古い選択項目に引きずられないように）。
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                string keyword = combo.Text ?? "";
                if (combo.IsDropDownOpen)
                {
                    combo.IsDropDownOpen = false;
                }

                await SearchByKeywordAsync(keyword).ConfigureAwait(true);
            }
        }

        // 検索実行処理
        public Task SearchByKeywordAsync(string keyword) =>
            SearchByKeywordAsync(keyword, addToHistory: true);

        public async Task SearchByKeywordAsync(string keyword, bool addToHistory)
        {
            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            CancelIncrementalSearchDebounce();
            string text = keyword ?? "";
            _historyCursor = -1;
            // Enter / タグ / 履歴選択はここで確定済み。TagBar は記録しない。
            // いずれも LostFocus で二重記録しないよう入力待ちフラグを下ろす。
            _pendingTypedSearchHistory = false;
            _isApplyingSearchKeyword = true;
            try
            {
                // 履歴リストを先に更新してから ComboBox へ反映しないと、
                // SelectedValue 不一致で Text が空になり全件表示へ戻ることがある。
                if (addToHistory && !string.IsNullOrEmpty(text))
                {
                    PromoteSearchHistory(text);
                }

                MainVM.DbInfo.SearchKeyword = text;
                SearchBox.Text = text;
                _sessionState.BumpFilterGeneration();
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

            // 特殊検索（例: {::error} や {tag = ''}）も通常検索と同様に履歴へ反映する。
            if (addToHistory && !string.IsNullOrEmpty(text))
            {
                string dbPath = MainVM.DbInfo.DBFullPath;
                _ = Task.Run(() => InsertHistoryTable(dbPath, text));
            }
        }

        private async Task DoSearchBoxSearch()
        {
            await SearchByKeywordAsync(SearchBox.Text).ConfigureAwait(true);
        }


        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateDetailFromSelection();

        private void SkinView_SelectionChanged(object sender, EventArgs e) =>
            UpdateDetailFromSelection();

        private void UpdateDetailFromSelection()
        {
            MovieRecords mv = GetSelectedMovie();
            if (mv == null)
            {
                viewExtDetail.Visibility = Visibility.Collapsed;
                return;
            }

            viewExtDetail.DataContext = mv;
            viewExtDetail.Visibility = Visibility.Visible;
            EnsureDetailThumbnail(mv);
        }

        private async void SkinView_PlayRequested(object sender, UserControls.SkinPlayRequestEventArgs e)
        {
            MovieRecords mv = filterList.FirstOrDefault(x => x.Movie_Id == e.MovieId);
            if (mv == null || string.IsNullOrWhiteSpace(mv.Movie_Path) || !Path.Exists(mv.Movie_Path))
            {
                return;
            }

            _skinThumbClickValid = true;
            _skinThumbClickMovieId = e.MovieId;
            _skinThumbClickOnImage = new System.Windows.Point(e.ClickX, e.ClickY);
            _skinThumbImageWidth = e.ImageWidth;
            _skinThumbImageHeight = e.ImageHeight;

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

            int secPos = 0;
            if (!TryResolvePlayPositionFromThumb(mv, _currentSkinEngine, out _, out int msec))
            {
                msec = GetPlayPosition(_currentSkinEngine, mv, ref secPos);
            }

            string moviePath = $"\"{mv.Movie_Path}\"";
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

        private async void SkinView_SearchTagRequested(object sender, (string Tag, bool Ctrl) e)
        {
            string keyword = e.Ctrl ? $"{SearchBox.Text} {e.Tag}" : e.Tag;
            await SearchByKeywordAsync(keyword).ConfigureAwait(true);
        }

        private void SkinView_RemoveTagRequested(object sender, (long MovieId, string Tag) e)
        {
            List<MovieRecords> selected = GetSelectedMovies() ?? [];
            List<MovieRecords> targets;
            if (selected.Exists(x => x.Movie_Id == e.MovieId))
            {
                targets = [.. selected.Where(m => m.Tag != null && m.Tag.Contains(e.Tag))];
            }
            else
            {
                MovieRecords mv = filterList.FirstOrDefault(x => x.Movie_Id == e.MovieId);
                targets = mv != null && mv.Tag != null && mv.Tag.Contains(e.Tag) ? [mv] : [];
            }

            if (targets.Count == 0)
            {
                return;
            }

            foreach (MovieRecords mv in targets)
            {
                TagMutationService.ApplyDelete(mv, e.Tag);
                UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, mv.Movie_Id, MovieColumn.Tag, mv.Tags);
            }

            Refresh();
        }

        private int GetPlayPosition(SkinEngine engine, MovieRecords mv, ref int returnPos)
        {
            if (_skinThumbClickValid
                && _skinThumbClickMovieId == mv.Movie_Id
                && _skinThumbImageWidth > 0
                && _skinThumbImageHeight > 0)
            {
                return PlayPositionResolver.GetPlayPositionMsec(
                    _skinThumbClickOnImage,
                    _skinThumbImageWidth,
                    _skinThumbImageHeight,
                    engine,
                    mv,
                    ref returnPos);
            }

            if (_lastThumbClickValid
                && _lastClickedThumbImage != null
                && _lastClickedThumbImage.ActualWidth > 0
                && _lastClickedThumbImage.ActualHeight > 0)
            {
                return PlayPositionResolver.GetPlayPositionMsec(
                    _lastThumbClickOnImage,
                    _lastClickedThumbImage.ActualWidth,
                    _lastClickedThumbImage.ActualHeight,
                    engine,
                    mv,
                    ref returnPos);
            }

            return 0;
        }

        private bool TryResolvePlayPositionFromThumb(MovieRecords mv, SkinEngine engine, out int panelIndex, out int positionMsec)
        {
            panelIndex = 0;
            positionMsec = 0;
            string thumbPath = PlayPositionResolver.GetThumbPathForEngine(mv, engine);

            if (_skinThumbClickValid
                && _skinThumbClickMovieId == mv.Movie_Id
                && _skinThumbImageWidth > 0
                && _skinThumbImageHeight > 0
                && ThumbPanelHitResolver.TryResolveFromImageClick(
                    _skinThumbClickOnImage,
                    _skinThumbImageWidth,
                    _skinThumbImageHeight,
                    thumbPath,
                    ZipMediaKind.IsZipRecord(mv),
                    out panelIndex,
                    out positionMsec))
            {
                return true;
            }

            if (_contextMenuThumbClickValid
                && _contextMenuThumbImage != null
                && _contextMenuThumbImage.ActualWidth > 0
                && _contextMenuThumbImage.ActualHeight > 0
                && ThumbPanelHitResolver.TryResolveFromImageClick(
                    _contextMenuThumbClick,
                    _contextMenuThumbImage.ActualWidth,
                    _contextMenuThumbImage.ActualHeight,
                    thumbPath,
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
                    thumbPath,
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
                    SelectMovieRecordPreservingMulti(_contextMenuMovie);
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
                SelectMovieRecordPreservingMulti(record);
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

            switch (_currentSkinEngine)
            {
                case SkinEngine.Wpf:
                    WpfSkinList.SelectedItem = record;
                    break;
                case SkinEngine.Wb:
                    SkinViewGridWb.SelectMovie(record, filterList);
                    break;
            }
        }

        /// <summary>
        /// 右クリック等。既に複数選択に含まれていれば選択を崩さずフォーカスだけ合わせる。
        /// </summary>
        private void SelectMovieRecordPreservingMulti(MovieRecords record)
        {
            if (record == null)
            {
                return;
            }

            switch (_currentSkinEngine)
            {
                case SkinEngine.Wpf:
                    if (WpfSkinList.SelectedItems.Contains(record))
                    {
                        return;
                    }

                    WpfSkinList.SelectedItem = record;
                    break;
                case SkinEngine.Wb:
                    SkinViewGridWb.FocusOrSelectMovie(record, filterList);
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

        public MovieRecords GetSelectedMovie() => TabSelectionHelper.GetSelectedItem(this);

        private List<MovieRecords> GetSelectedMovies() => TabSelectionHelper.GetSelectedItems(this);

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // senderがLabelで、DataContextがMovieRecordsであることを確認
            if (sender is Label label && label.DataContext is MovieRecords record)
            {
                // DataGridの選択状態を強制的にセット
                WpfSkinList.SelectedItem = record;

                // sources 同居のジャケ枠は先頭再生（格子ヒットしない）
                if (label.Tag as string == WpfSkinThumbnailSources.JacketPlaySlotTag)
                {
                    lbClickPoint = new System.Windows.Point(0, 0);
                    _lastClickedThumbImage = null;
                    _lastThumbClickValid = false;
                    return;
                }

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

        private void MovieListHost_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsMovieListActive) { return; }

            if (_currentSkinEngine == SkinEngine.Wb
                && e.Key is Key.Home or Key.End)
            {
                TabSelectionHelper.GetSkinView(this)
                    ?.ForwardKeyNav(e.Key, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
                e.Handled = true;
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:                         //再生
                    PlayMovie_Click(sender, e); break;
                case Key.F6:                            //タグ編集
                    TagEdit_Click(sender, e); break;
                case Key.C:                             //タグのコピー（Ctrl/Alt 併用時は OS 側に任せる）
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
                    {
                        return;
                    }

                    TagCopy_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.V:                             //タグの貼り付け
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
                    {
                        return;
                    }

                    TagPaste_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Add:                           //スコアプラス
                case Key.Subtract:                      //スコアマイナス
                    MenuScore_Click(sender, e);
                    break;
                case Key.Delete:                        //登録の削除 / Shift+Delete でファイル削除
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
                        if (id == "28")
                        {
                            SortDefinitions.ReseedRandom();
                            InvalidateAllItemsFilterCache();
                        }

                        MainVM.DbInfo.Sort = id;
                        await ApplyFilterAndSortAsync(id).ConfigureAwait(true);
                        SelectFirstItem();
                    }
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
                        return (generation, dbPath, "", []);
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

            FolderCheckProgressSession folderCheckProgress =
                await BeginFolderCheckProgressAsync(foldersToCheck.Count).ConfigureAwait(true);

            MoviePathRegistrationIndex pathIndex = await Task.Run(() =>
                MoviePathRegistrationIndex.Load(dbFullPath)).ConfigureAwait(false);

            FolderCheckScanResult scanResult;
            try
            {
                scanResult = await FolderCheckService.ScanAndRegisterAsync(
                    dbFullPath,
                    foldersToCheck,
                    excludeExt,
                    pathIndex,
                    new FolderCheckScanCallbacks
                    {
                        IsStillActive = folderCheckStillActive,
                        TryEnterRegistrationGate = path => _discoveredFileRegistrationGate.TryEnter(path),
                        ExitRegistrationGate = path => _discoveredFileRegistrationGate.Exit(path),
                        OnMovieRegistered = CancelThumbnailWorkForMovie,
                        ReportProgressAsync = (done, detail) =>
                            ReportFolderCheckProgressAsync(folderCheckProgress, done, detail),
                    }).ConfigureAwait(false);
            }
            finally
            {
                await EndFolderCheckProgressAsync(folderCheckProgress).ConfigureAwait(true);
            }

            if (!folderCheckStillActive()
                || !FolderCheckService.ShouldApplyResults(
                    scanResult.RegisteredAny,
                    scanResult.FoundUnregistered))
            {
                return;
            }

            List<QueueObj> addFiles = scanResult.AddedThumbnailWork;
            bool registeredAny = scanResult.RegisteredAny;

            await Dispatcher.InvokeAsync(async () =>
            {
                if (!folderCheckStillActive())
                {
                    return;
                }

                string sortId = MainVM.DbInfo.Sort ?? "1";
                await FilterAndSortAsync(sortId, true).ConfigureAwait(true);

                if (!folderCheckStillActive())
                {
                    return;
                }

                if (registeredAny && addFiles.Count > 0)
                {
                    foreach (QueueObj item in addFiles)
                    {
                        PopulateActiveListQueueLayout(item);
                    }

                    EnqueueThumbnailWork(
                        addFiles,
                        beginNewJob: ShouldBeginNewDiscoveredThumbnailJob());
                    EnqueueExtraSkinThumbsForNewMovies(addFiles);
                    EnqueueAutoDmmFetchForDiscovered(addFiles);
                    EnqueueSinkuRefreshForDiscovered(addFiles);
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
            try
            {
                await BookmarkThumbnailCreator.CreateAsync(movieFullPath, saveThumbPath, capturePos).ConfigureAwait(true);
            }
            finally
            {
                _bookmarkThumbInFlight.Remove(saveThumbPath);
                BookmarkList.Items.Refresh();
            }
        }

        private ThumbnailHashSyncContext CreateThumbnailHashSyncContext() =>
            ThumbnailHashSync.ForDatabase(MainVM?.DbInfo?.DBFullPath);

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
                UpdateMovieColumn = (dbPath, movieId, value) =>
                    UpdateMovieSingleColumn(dbPath, movieId, "movie_length", value),
                HashSyncContext = ThumbnailHashSync.ForDatabase(capturedDbFullPath),
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
            if (!ThumbnailFailurePlaceholder.TryWrite(_thumbLayoutCache, queueObj, saveThumbFileName))
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
            // 他スキン事前生成などの完了をカレント一覧の ThumbPath* に流し込まない
            if (!ThumbPathHelper.ShouldApplyToVisibleUi(queueObj, GetActiveListLayoutKey()))
            {
                return;
            }

            ThumbPathHelper.ApplyThumbPaths(MainVM.MovieRecs, queueObj, saveThumbFileName, _currentSkinEngine);

            if (queueObj.ThumbnailLayout != null
                && !queueObj.ThumbnailLayout.Equals(ThumbnailLayoutSpec.DetailPaneLayout))
            {
                MovieRecords mv = MainVM.MovieRecs.FirstOrDefault(x => x.Movie_Id == queueObj.MovieId);
                if (mv != null)
                {
                    EnsureDetailThumbnail(mv);
                }

                if (_currentSkinEngine == SkinEngine.Wb
                    && queueObj.ThumbnailLayout != null
                    && queueObj.ThumbnailLayout.Equals(WhiteBrowserSkinSettings.GetThumbnailLayoutSpec()))
                {
                    MovieRecords webMv = mv ?? MainVM.MovieRecs.FirstOrDefault(x => x.Movie_Id == queueObj.MovieId);
                    if (webMv != null)
                    {
                        UserControls.SkinView skinView = TabSelectionHelper.GetSkinView(this);
                        skinView?.UpdateThumb(webMv.Movie_Id, webMv.ThumbPathWb);
                    }
                }

                // WPF スキンのカードは ThumbPathWpfSkin の INotifyPropertyChanged で個別に更新される。
                // 1 件ごとに WpfSkinList.Items.Refresh() を呼ぶと ListView 全体が再生成され、
                // 大量生成中に UI スレッドを占有してタブ切替が重くなるため呼ばない。
            }
        }

        /// <summary>
        /// 手動等間隔サムネイル作成
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateThumb_EqualInterval(object sender, RoutedEventArgs e)
        {
            if (!IsMovieListActive) { return; }

            // 複数選択対応: 選択中の全アイテムを取得
            List<MovieRecords> selectedItems = GetSelectedMovies();
            if (selectedItems == null || selectedItems.Count == 0) { return; }

            List<QueueObj> thumbQueue = [.. selectedItems.Select(mv =>
            {
                var queueItem = new QueueObj
                {
                    MovieId = mv.Movie_Id,
                    MovieFullPath = mv.Movie_Path,
                };
                PopulateActiveListQueueLayout(queueItem);
                return queueItem;
            })];
            EnqueueThumbnailWork(thumbQueue, beginNewJob: true);
        }


        ComboBox IMainWindowActions.SearchBox => SearchBox;
        SkinEngine IMainWindowActions.CurrentSkinEngine => _currentSkinEngine;
        bool IMainWindowActions.IsMovieListActive => IsMovieListActive;
        bool IMainWindowListViews.IsMovieListActive => IsMovieListActive;
        SkinEngine IMainWindowListViews.CurrentSkinEngine => _currentSkinEngine;
        string IMainWindowActions.DbFullPath => MainVM.DbInfo.DBFullPath;
        ListView IMainWindowListViews.WpfSkinList => WpfSkinList;
        UserControls.SkinView IMainWindowListViews.SkinViewGridWb => SkinViewGridWb;

        void IMainWindowActions.RefreshExtDetail() => viewExtDetail.Refresh();

        void IMainWindowActions.RequestDetailThumbnailRecreate() => RequestDetailThumbnailRecreate();

        void IMainWindowActions.OpenMetadataEdit() => OpenMetadataEdit();

        void IMainWindowActions.RefreshActiveList(SkinEngine engine) =>
            TabListRefreshHelper.RefreshActiveList(engine, this);

        void IMainWindowActions.UpdateMovieColumn(long movieId, MovieColumn column, object value) =>
            UpdateMovieSingleColumn(MainVM.DbInfo.DBFullPath, movieId, column, value);

        IReadOnlyList<MovieRecords> IMainWindowActions.GetSelectedMovies() =>
            GetSelectedMovies() ?? [];

        UserControls.ExtDetail IMainWindowListHost.ViewExtDetail => viewExtDetail;

    }
}
