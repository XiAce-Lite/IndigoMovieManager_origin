using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Microsoft.Web.WebView2.Core;

namespace IndigoMovieManager.UserControls
{
    public enum SkinViewMode
    {
        SmallWeb,
        GridWeb,
    }

    public sealed class SkinPlayRequestEventArgs : EventArgs
    {
        public long MovieId { get; init; }
        public double ClickX { get; init; }
        public double ClickY { get; init; }
        public double ImageWidth { get; init; }
        public double ImageHeight { get; init; }
        public int Start { get; init; }
    }

    public partial class SkinView : UserControl
    {
        private const string ThumbVirtualHost = "imm-thumb.local";
        private const string ImagesVirtualHost = "imm-images.local";
        private const string SkinVirtualHost = "imm-skin.local";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private bool _initialized;
        private bool _ready;
        private bool IsWhiteBrowserCompat => SkinTabIndexHelper.IsWhiteBrowserCompatTab(SkinTabIndex);
        private string _thumbRoot = "";
        private string _imagesRoot = "";
        private IEnumerable<MovieRecords> _lastItems;
        private readonly HashSet<long> _selectedIds = [];
        private long? _focusedId;
        private bool _focusPending;
        private int _renderGeneration;
        private SkinConfig _config;
        private SkinConfig _expectedConfig;

        private const int RenderFirstBatchSize = 48;
        private const int RenderAppendBatchSize = 240;

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(
                nameof(Mode),
                typeof(SkinViewMode),
                typeof(SkinView),
                new PropertyMetadata(SkinViewMode.SmallWeb));

        public static readonly DependencyProperty SkinTabIndexProperty =
            DependencyProperty.Register(
                nameof(SkinTabIndex),
                typeof(int),
                typeof(SkinView),
                new PropertyMetadata(SkinTabIndexHelper.WbSkinTabIndex));

        public SkinViewMode Mode
        {
            get => (SkinViewMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public int SkinTabIndex
        {
            get => (int)GetValue(SkinTabIndexProperty);
            set => SetValue(SkinTabIndexProperty, value);
        }

        internal SkinConfig Config => _config;

        public IReadOnlyCollection<long> SelectedIds => _selectedIds;

        public long? FocusedId => _focusedId;

        public event EventHandler SelectionChanged;
        public event EventHandler<SkinPlayRequestEventArgs> PlayRequested;
        public event EventHandler<(string Tag, bool Ctrl)> SearchTagRequested;
        public event EventHandler<(long MovieId, string Tag)> RemoveTagRequested;

        public SkinView()
        {
            InitializeComponent();
            Loaded += SkinView_Loaded;
        }

        private async void SkinView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                return;
            }

            _expectedConfig = SkinTabIndexHelper.GetDefaultConfig(SkinTabIndex);
            if (IsWhiteBrowserCompat)
            {
                _expectedConfig = WhiteBrowserSkinSettings.ParseSkinConfig(WhiteBrowserSkinSettings.ActiveSkinFolder);
            }
            await EnsureInitializedAsync().ConfigureAwait(true);
        }

        public async Task EnsureInitializedAsync(string thumbRoot = null, string imagesRoot = null)
        {
            if (!string.IsNullOrWhiteSpace(thumbRoot))
            {
                _thumbRoot = thumbRoot;
            }

            if (!string.IsNullOrWhiteSpace(imagesRoot))
            {
                _imagesRoot = imagesRoot;
            }

            if (_initialized)
            {
                return;
            }

            try
            {
                await Browser.EnsureCoreWebView2Async().ConfigureAwait(true);
                CoreWebView2 core = Browser.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.WebMessageReceived += Core_WebMessageReceived;
                Browser.PreviewKeyDown += Browser_PreviewKeyDown;

                if (!string.IsNullOrWhiteSpace(_thumbRoot) && Directory.Exists(_thumbRoot))
                {
                    core.SetVirtualHostNameToFolderMapping(
                        ThumbVirtualHost,
                        _thumbRoot,
                        CoreWebView2HostResourceAccessKind.Allow);
                }

                if (!string.IsNullOrWhiteSpace(_imagesRoot) && Directory.Exists(_imagesRoot))
                {
                    core.SetVirtualHostNameToFolderMapping(
                        ImagesVirtualHost,
                        _imagesRoot,
                        CoreWebView2HostResourceAccessKind.Allow);
                }

                if (IsWhiteBrowserCompat)
                {
                    string wbRoot = WhiteBrowserSkinSettings.GetWbHostRoot();
                    if (Directory.Exists(wbRoot))
                    {
                        core.SetVirtualHostNameToFolderMapping(
                            WhiteBrowserSkinSettings.WbHostVirtualHost,
                            wbRoot,
                            CoreWebView2HostResourceAccessKind.Allow);
                    }

                    string compatScript = WhiteBrowserSkinSettings.GetCompatScriptPath();
                    if (File.Exists(compatScript))
                    {
                        string script = await File.ReadAllTextAsync(compatScript).ConfigureAwait(true);
                        await core.AddScriptToExecuteOnDocumentCreatedAsync(script).ConfigureAwait(true);
                    }

                    core.Navigate(WhiteBrowserSkinSettings.GetEntryUrl());
                }
                else
                {
                    string skinsRoot = Path.Combine(AppContext.BaseDirectory, "Skins");
                    if (Directory.Exists(skinsRoot))
                    {
                        core.SetVirtualHostNameToFolderMapping(
                            SkinVirtualHost,
                            skinsRoot,
                            CoreWebView2HostResourceAccessKind.Allow);
                    }

                    string folder = SkinTabIndexHelper.GetSkinFolderName(SkinTabIndex);
                    string entry = $"https://{SkinVirtualHost}/{folder}/{folder}.htm";
                    core.Navigate(entry);
                }

                _initialized = true;
                ErrorText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"WebView2 の初期化に失敗しました: {ex.Message}";
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        public void UpdateHostMappings(string thumbRoot, string imagesRoot)
        {
            _thumbRoot = thumbRoot ?? "";
            _imagesRoot = imagesRoot ?? ApplicationPaths.ImagesDirectory;

            if (!_initialized || Browser.CoreWebView2 == null)
            {
                return;
            }

            CoreWebView2 core = Browser.CoreWebView2;
            if (!string.IsNullOrWhiteSpace(_thumbRoot) && Directory.Exists(_thumbRoot))
            {
                core.SetVirtualHostNameToFolderMapping(
                    ThumbVirtualHost,
                    _thumbRoot,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            if (!string.IsNullOrWhiteSpace(_imagesRoot) && Directory.Exists(_imagesRoot))
            {
                core.SetVirtualHostNameToFolderMapping(
                    ImagesVirtualHost,
                    _imagesRoot,
                    CoreWebView2HostResourceAccessKind.Allow);
            }
        }

        public async Task ReloadWhiteBrowserSkinAsync()
        {
            if (!IsWhiteBrowserCompat)
            {
                return;
            }

            _expectedConfig = WhiteBrowserSkinSettings.ParseSkinConfig(WhiteBrowserSkinSettings.ActiveSkinFolder);
            _ready = false;
            _renderGeneration++;

            if (!_initialized || Browser.CoreWebView2 == null)
            {
                await EnsureInitializedAsync().ConfigureAwait(true);
                return;
            }

            Browser.CoreWebView2.Navigate(WhiteBrowserSkinSettings.GetEntryUrl());
        }

        public void RenderItems(IEnumerable<MovieRecords> items)
        {
            if (items == null)
            {
                return;
            }

            _lastItems = items;

            if (!_ready)
            {
                return;
            }

            int generation = ++_renderGeneration;
            IReadOnlyList<MovieRecords> list = items as IReadOnlyList<MovieRecords> ?? items.ToArray();
            long[] selectedIds = _selectedIds.ToArray();
            long? focusedId = _focusedId;

            if (IsWhiteBrowserCompat)
            {
                if (list.Count <= RenderFirstBatchSize)
                {
                    PostWbRenderMessage(generation, list, 0, list.Count, selectedIds, focusedId, reset: true);
                    return;
                }

                PostWbRenderMessage(generation, list, 0, RenderFirstBatchSize, selectedIds, focusedId, reset: true);
                ScheduleWbRenderAppend(generation, list, RenderFirstBatchSize, selectedIds, focusedId);
                return;
            }

            object config = ToJsConfig(_config ?? _expectedConfig);

            if (list.Count <= RenderFirstBatchSize)
            {
                PostRenderMessage(generation, config, list, 0, list.Count, selectedIds, focusedId, complete: true);
                return;
            }

            PostRenderMessage(generation, config, list, 0, RenderFirstBatchSize, selectedIds, focusedId, complete: false);
            ScheduleRenderAppend(generation, config, list, RenderFirstBatchSize, selectedIds, focusedId);
        }

        private void ScheduleWbRenderAppend(
            int generation,
            IReadOnlyList<MovieRecords> list,
            int offset,
            long[] selectedIds,
            long? focusedId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (generation != _renderGeneration || !_ready || Browser.CoreWebView2 == null)
                {
                    return;
                }

                int count = Math.Min(RenderAppendBatchSize, list.Count - offset);
                if (count <= 0)
                {
                    return;
                }

                int nextOffset = offset + count;
                PostWbRenderMessage(generation, list, offset, count, selectedIds, focusedId, reset: false);

                if (nextOffset < list.Count)
                {
                    ScheduleWbRenderAppend(generation, list, nextOffset, selectedIds, focusedId);
                }
            }, DispatcherPriority.Background);
        }

        private void PostWbRenderMessage(
            int generation,
            IReadOnlyList<MovieRecords> list,
            int offset,
            int count,
            long[] selectedIds,
            long? focusedId,
            bool reset)
        {
            if (generation != _renderGeneration || Browser.CoreWebView2 == null)
            {
                return;
            }

            var dtoItems = MapDtoRange(list, offset, count);
            var payload = new
            {
                type = "wbRender",
                items = dtoItems,
                reset,
                selectedIds,
                focusedId,
            };
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private void ScheduleRenderAppend(
            int generation,
            object config,
            IReadOnlyList<MovieRecords> list,
            int offset,
            long[] selectedIds,
            long? focusedId)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (generation != _renderGeneration || !_ready || Browser.CoreWebView2 == null)
                {
                    return;
                }

                int count = Math.Min(RenderAppendBatchSize, list.Count - offset);
                if (count <= 0)
                {
                    return;
                }

                int nextOffset = offset + count;
                bool complete = nextOffset >= list.Count;
                PostRenderAppend(generation, config, list, offset, count, selectedIds, focusedId, complete);

                if (!complete)
                {
                    ScheduleRenderAppend(generation, config, list, nextOffset, selectedIds, focusedId);
                }
            }, DispatcherPriority.Background);
        }

        private void PostRenderMessage(
            int generation,
            object config,
            IReadOnlyList<MovieRecords> list,
            int offset,
            int count,
            long[] selectedIds,
            long? focusedId,
            bool complete)
        {
            if (generation != _renderGeneration || Browser.CoreWebView2 == null)
            {
                return;
            }

            var dtoItems = MapDtoRange(list, offset, count);
            var payload = new
            {
                type = "render",
                config,
                items = dtoItems,
                selectedIds,
                focusedId,
                partial = !complete,
                total = list.Count,
            };
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private void PostRenderAppend(
            int generation,
            object config,
            IReadOnlyList<MovieRecords> list,
            int offset,
            int count,
            long[] selectedIds,
            long? focusedId,
            bool complete)
        {
            if (generation != _renderGeneration || Browser.CoreWebView2 == null)
            {
                return;
            }

            var dtoItems = MapDtoRange(list, offset, count);
            var payload = new
            {
                type = "renderAppend",
                items = dtoItems,
                complete,
            };
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private object[] MapDtoRange(IReadOnlyList<MovieRecords> list, int offset, int count)
        {
            var dtoItems = new object[count];
            for (int i = 0; i < count; i++)
            {
                MovieRecords rec = list[offset + i];
                dtoItems[i] = IsWhiteBrowserCompat
                    ? SkinMovieMapper.ToWhiteBrowserDto(rec, SkinTabIndex, MapThumbUrl, _selectedIds, _focusedId)
                    : SkinMovieMapper.ToDto(rec, SkinTabIndex, MapThumbUrl, _selectedIds, _focusedId);
            }

            return dtoItems;
        }

        public void FocusContent()
        {
            if (_ready && Browser.CoreWebView2 != null)
            {
                Browser.Focus();
            }
            else
            {
                _focusPending = true;
            }
        }

        public void UpdateThumb(long movieId, string thumbFullPath)
        {
            if (!_ready || Browser.CoreWebView2 == null)
            {
                return;
            }

            if (IsWhiteBrowserCompat)
            {
                var payload = new { type = "wbUpdateThum", id = movieId, thum = MapThumbUrl(thumbFullPath) };
                Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
            }
            else
            {
                var payload = new { type = "updateThumb", id = movieId, thumb = MapThumbUrl(thumbFullPath) };
                Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
            }
        }

        public void SelectFirstItem(IEnumerable<MovieRecords> items)
        {
            MovieRecords first = items?.FirstOrDefault();
            if (first == null)
            {
                _selectedIds.Clear();
                _focusedId = null;
                PostSelectionOnly();
                return;
            }

            _selectedIds.Clear();
            _selectedIds.Add(first.Movie_Id);
            _focusedId = first.Movie_Id;
            RenderItems(items);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectMovie(MovieRecords record, IEnumerable<MovieRecords> items)
        {
            if (record == null)
            {
                _selectedIds.Clear();
                _focusedId = null;
                if (items != null)
                {
                    RenderItems(items);
                }
                else
                {
                    PostSelectionOnly();
                }

                return;
            }

            _selectedIds.Clear();
            _selectedIds.Add(record.Movie_Id);
            _focusedId = record.Movie_Id;
            if (items != null)
            {
                RenderItems(items);
            }
            else
            {
                PostSelectionOnly();
            }
        }

        public MovieRecords GetPrimarySelection(IEnumerable<MovieRecords> source)
        {
            if (source == null || _focusedId == null)
            {
                return source?.FirstOrDefault(x => _selectedIds.Contains(x.Movie_Id));
            }

            return source.FirstOrDefault(x => x.Movie_Id == _focusedId)
                ?? source.FirstOrDefault(x => _selectedIds.Contains(x.Movie_Id));
        }

        public List<MovieRecords> GetSelectedItems(IEnumerable<MovieRecords> source)
        {
            if (source == null || _selectedIds.Count == 0)
            {
                return [];
            }

            return [.. source.Where(x => _selectedIds.Contains(x.Movie_Id))];
        }

        private void Browser_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is not Key.Home and not Key.End)
            {
                return;
            }

            // Home/End は WebView2 から WPF へ転送される。Handled にして TabControl のタブ切替を止め、JS へ送る。
            e.Handled = true;
            ForwardKeyNav(e.Key, (Keyboard.Modifiers & ModifierKeys.Control) != 0);
        }

        public void ForwardKeyNav(Key key, bool ctrl)
        {
            if (!_ready || Browser.CoreWebView2 == null || key is not Key.Home and not Key.End)
            {
                return;
            }

            var payload = new
            {
                type = "keyNav",
                key = key == Key.Home ? "Home" : "End",
                ctrl,
            };
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
        }

        private void Core_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("type", out JsonElement typeElement))
                {
                    return;
                }

                string type = typeElement.GetString();
                switch (type)
                {
                    case "ready":
                        ApplyConfigFromMessage(root);
                        _ready = true;
                        if (_lastItems != null)
                        {
                            RenderItems(_lastItems);
                        }
                        if (_focusPending)
                        {
                            _focusPending = false;
                            Browser.Focus();
                        }
                        break;
                    case "select":
                        ApplySelectionFromMessage(root);
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                        break;
                    case "play":
                    {
                        long movieId = root.GetProperty("id").GetInt64();
                        int start = root.TryGetProperty("start", out JsonElement startEl) ? startEl.GetInt32() : 0;
                        double clickX = 0;
                        double clickY = 0;
                        double imgWidth = 0;
                        double imgHeight = 0;
                        if (root.TryGetProperty("clickX", out JsonElement cx)) clickX = cx.GetDouble();
                        if (root.TryGetProperty("clickY", out JsonElement cy)) clickY = cy.GetDouble();
                        if (root.TryGetProperty("imgWidth", out JsonElement iw)) imgWidth = iw.GetDouble();
                        if (root.TryGetProperty("imgHeight", out JsonElement ih)) imgHeight = ih.GetDouble();
                        PlayRequested?.Invoke(this, new SkinPlayRequestEventArgs
                        {
                            MovieId = movieId,
                            ClickX = clickX,
                            ClickY = clickY,
                            ImageWidth = imgWidth,
                            ImageHeight = imgHeight,
                            Start = start,
                        });
                        break;
                    }
                    case "searchTag":
                        SearchTagRequested?.Invoke(
                            this,
                            (root.GetProperty("tag").GetString(), root.TryGetProperty("ctrl", out JsonElement c) && c.GetBoolean()));
                        break;
                    case "removeTag":
                        RemoveTagRequested?.Invoke(
                            this,
                            (root.GetProperty("id").GetInt64(), root.GetProperty("tag").GetString()));
                        break;
                }
            }
            catch
            {
                // Webメッセージの不正形式は無視
            }
        }

        private void ApplyConfigFromMessage(JsonElement root)
        {
            if (IsWhiteBrowserCompat)
            {
                _config = _expectedConfig;
                return;
            }

            if (!root.TryGetProperty("config", out JsonElement cfg))
            {
                _config = _expectedConfig;
                return;
            }

            var parsed = new SkinConfig
            {
                SkinVersion = cfg.TryGetProperty("skinVersion", out JsonElement sv) ? sv.GetInt32() : 1,
                ThumbWidth = cfg.TryGetProperty("thumbWidth", out JsonElement tw) ? tw.GetInt32() : _expectedConfig.ThumbWidth,
                ThumbHeight = cfg.TryGetProperty("thumbHeight", out JsonElement th) ? th.GetInt32() : _expectedConfig.ThumbHeight,
                ThumbColumn = cfg.TryGetProperty("thumbColumn", out JsonElement tc) ? tc.GetInt32() : _expectedConfig.ThumbColumn,
                ThumbRow = cfg.TryGetProperty("thumbRow", out JsonElement tr) ? tr.GetInt32() : _expectedConfig.ThumbRow,
                MultiSelect = cfg.TryGetProperty("multiSelect", out JsonElement ms) ? ms.GetInt32() : 1,
                SeamlessScroll = cfg.TryGetProperty("seamlessScroll", out JsonElement ss) ? ss.GetInt32() : 0,
                ScrollId = cfg.TryGetProperty("scrollId", out JsonElement sid) ? sid.GetString() : "view",
            }.WithFallback(_expectedConfig);

            if (!parsed.Matches(_expectedConfig))
            {
                parsed = _expectedConfig;
            }

            _config = parsed;
        }

        private void ApplySelectionFromMessage(JsonElement root)
        {
            _selectedIds.Clear();
            if (root.TryGetProperty("ids", out JsonElement idsElement) && idsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement idElement in idsElement.EnumerateArray())
                {
                    _selectedIds.Add(idElement.GetInt64());
                }
            }

            if (root.TryGetProperty("focusedId", out JsonElement focusedElement)
                && focusedElement.ValueKind != JsonValueKind.Null)
            {
                _focusedId = focusedElement.GetInt64();
            }
            else if (_selectedIds.Count > 0)
            {
                _focusedId = _selectedIds.Last();
            }
            else
            {
                _focusedId = null;
            }
        }

        private void PostSelectionOnly()
        {
            if (!_ready || Browser.CoreWebView2 == null)
            {
                return;
            }

            if (IsWhiteBrowserCompat)
            {
                var payload = new { type = "wbSelection", ids = _selectedIds.ToArray(), focusedId = _focusedId };
                Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
            }
            else
            {
                var payload = new { type = "selection", ids = _selectedIds.ToArray(), focusedId = _focusedId };
                Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, JsonOptions));
            }
        }

        private string MapThumbUrl(string fullPath)
        {
            string url = SkinMovieMapper.ToVirtualThumbUrl(fullPath, _thumbRoot, ThumbVirtualHost);
            if (!string.IsNullOrEmpty(url))
            {
                return url;
            }

            return SkinMovieMapper.ToVirtualImageUrl(fullPath, _imagesRoot, ImagesVirtualHost);
        }

        private static object ToJsConfig(SkinConfig config) => new
        {
            skinVersion = config.SkinVersion,
            thumbWidth = config.ThumbWidth,
            thumbHeight = config.ThumbHeight,
            thumbColumn = config.ThumbColumn,
            thumbRow = config.ThumbRow,
            multiSelect = config.MultiSelect,
            seamlessScroll = config.SeamlessScroll,
            scrollId = config.ScrollId,
            sheetWidth = config.SheetWidth,
            sheetHeight = config.SheetHeight,
        };
    }
}
