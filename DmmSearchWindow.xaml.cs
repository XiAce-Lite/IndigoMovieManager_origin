using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;
using MaterialDesignThemes.Wpf;
using WpfDataGridTextColumn = System.Windows.Controls.DataGridTextColumn;

namespace IndigoMovieManager
{
    public sealed class DmmSearchWindowModel
    {
        public string TargetFileName { get; set; }
    }

    /// <summary>
    /// DMM 候補の検索・選択ダイアログ。
    /// </summary>
    public partial class DmmSearchWindow : Window
    {
        private enum GridListMode
        {
            Candidates,
            RescueUrls,
        }

        private const string KeywordHintCandidates =
            "検索語（CID・商品URL・品番。揺れは下の候補をクリック）";
        private const string KeywordHintRescue = "CID をそのまま入力（例: abcd00123 / h_000abcd00123）。直して再度ジャケ救済";

        /// <summary>手動検索の1ページ件数（0620c02 以降の候補30件方針に合わせる）。</summary>
        private const int PageHits = 30;

        private readonly MovieRecords _record;
        private readonly string _dbPath;
        private readonly long? _pendingId;
        private readonly bool _openedWithInitialCandidates;
        private readonly ObservableCollection<DmmCandidateRow> _candidates = [];
        private readonly ObservableCollection<DmmJacketGuessRow> _rescueRows = [];
        private readonly DmmMetadataApplyService _applier = new();
        private DmmMetadataResolveService _resolver;
        private bool _isSearching;
        private GridListMode _listMode = GridListMode.Candidates;
        private string _rescueJacketUrl;
        /// <summary>親画面のダブルクリック MouseUp/Down がセル操作に化けるのを抑止する。</summary>
        private DateTime _suppressCellClickUntilUtc;
        private const int SuppressOpenClickMs = 800;
        private int _nextOffset = 1;
        private bool _mayHaveMore;
        private string _lastSearchKeyword = string.Empty;
        private int _previewGeneration;

        public bool AppliedSuccessfully { get; private set; }

        internal DmmSearchWindow(
            MovieRecords record,
            string dbPath,
            string initialKeyword,
            IReadOnlyList<DmmCandidateEntry> initialCandidates = null,
            long? pendingId = null)
        {
            InitializeComponent();

            _record = record ?? throw new ArgumentNullException(nameof(record));
            _dbPath = dbPath ?? string.Empty;
            _pendingId = pendingId;
            _openedWithInitialCandidates = initialCandidates is { Count: > 0 };

            DataContext = new DmmSearchWindowModel
            {
                TargetFileName = record.Movie_Name ?? record.Movie_Path ?? "(不明)",
            };

            KeywordBox.Text = initialKeyword ?? string.Empty;
            ApplyListMode(GridListMode.Candidates, clearRescueSelection: true);
            PopulateVariantChips();

            if (_openedWithInitialCandidates)
            {
                SetCandidates(initialCandidates);
                StatusText.Text = $"{_candidates.Count} 件の候補を表示しています。";
                ArmOpenClickSuppress();
                _lastSearchKeyword = (initialKeyword ?? string.Empty).Trim();
                // 既存候補のあとの「次の30件」は offset=1 から取得し、重複は追記側で除外する。
                _nextOffset = 1;
                _mayHaveMore = !string.IsNullOrWhiteSpace(_lastSearchKeyword);
                UpdateNextPageEnabled();
            }
            else if (_pendingId.HasValue)
            {
                // 候補0件の未確定から開いた場合も、親のダブルクリック MouseUp を吸収する。
                // 保存時ゼロ件の再検索はしない（別語検索／ジャケ救済はユーザー起点）。
                ArmOpenClickSuppress();
                StatusText.Text = "保存時は候補 0 件です。別キーワードで検索するか、ジャケ救済を使ってください。";
            }

            Loaded += DmmSearchWindow_Loaded;
        }

        private void ArmOpenClickSuppress()
        {
            _suppressCellClickUntilUtc = DateTime.UtcNow.AddMilliseconds(SuppressOpenClickMs);
        }

        private bool IsOpenClickSuppressed => DateTime.UtcNow < _suppressCellClickUntilUtc;

        private void PopulateVariantChips()
        {
            VariantChipsPanel.Children.Clear();
            IReadOnlyList<string> variants = DmmInitialKeyword.SuggestSearchVariants(_record.Movie_Name);
            if (variants.Count == 0)
            {
                VariantChipsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            VariantChipsPanel.Visibility = Visibility.Visible;
            foreach (string variant in variants)
            {
                var chip = new Button
                {
                    Content = variant,
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 2, 8, 2),
                    Tag = variant,
                    ToolTip = "クリックでこの語句を検索",
                    Style = (Style)FindResource("MaterialDesignOutlinedButton"),
                };
                chip.Click += VariantChip_Click;
                VariantChipsPanel.Children.Add(chip);
            }
        }

        private async void VariantChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string variant)
            {
                KeywordBox.Text = variant;
                await RunSearchAsync(variant, append: false).ConfigureAwait(true);
            }
        }

        private async void DmmSearchWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ShowDialog 直後に届く親の MouseUp 用に、表示タイミングでも抑止を延長する
            if (_openedWithInitialCandidates || _pendingId.HasValue)
            {
                ArmOpenClickSuppress();
            }

            if (_candidates.Count > 0)
            {
                SelectPreferredCandidate();
                await UpdateJacketPreviewAsync(GetSelectedCandidateRow()).ConfigureAwait(true);
                return;
            }

            // 未確定（候補0件）は一括／初回検索の結果を尊重し、開いた瞬間の再検索をしない。
            if (_pendingId.HasValue)
            {
                return;
            }

            await RunSearchAsync(KeywordBox.Text, append: false).ConfigureAwait(true);
        }

        private void ApplyListMode(GridListMode mode, bool clearRescueSelection)
        {
            _listMode = mode;
            if (clearRescueSelection)
            {
                _rescueJacketUrl = null;
            }

            CandidatesGrid.Columns.Clear();
            if (mode == GridListMode.Candidates)
            {
                ModeLabelText.Text = "表示: DMM 候補";
                HintAssist.SetHint(KeywordBox, KeywordHintCandidates);
                CandidatesGrid.IsReadOnly = true;
                CandidatesGrid.BeginningEdit -= CandidatesGrid_BeginningEdit;
                CandidatesGrid.CellEditEnding -= CandidatesGrid_CellEditEnding;
                AddCandidateColumns();
                CandidatesGrid.ItemsSource = _candidates;
            }
            else
            {
                ModeLabelText.Text = "表示: ジャケ推定 URL（URL 列は編集可）";
                HintAssist.SetHint(KeywordBox, KeywordHintRescue);
                CandidatesGrid.IsReadOnly = false;
                CandidatesGrid.BeginningEdit -= CandidatesGrid_BeginningEdit;
                CandidatesGrid.CellEditEnding -= CandidatesGrid_CellEditEnding;
                CandidatesGrid.BeginningEdit += CandidatesGrid_BeginningEdit;
                CandidatesGrid.CellEditEnding += CandidatesGrid_CellEditEnding;
                AddRescueColumns();
                CandidatesGrid.ItemsSource = _rescueRows;
            }

            UpdateModeDependentButtons();
        }

        private void AddCandidateColumns()
        {
            CandidatesGrid.Columns.Add(new WpfDataGridTextColumn
            {
                Header = "ジャケ",
                Binding = new Binding(nameof(DmmCandidateRow.JacketLabel)),
                Width = new DataGridLength(56),
                MinWidth = 48,
                IsReadOnly = true,
            });
            CandidatesGrid.Columns.Add(CreateEllipsisColumn(
                "タイトル",
                nameof(DmmCandidateRow.Title),
                new DataGridLength(2.4, DataGridLengthUnitType.Star),
                140));
            CandidatesGrid.Columns.Add(CreateEllipsisColumn(
                "品番",
                nameof(DmmCandidateRow.ContentId),
                new DataGridLength(140),
                100));
            CandidatesGrid.Columns.Add(CreateEllipsisColumn(
                "メーカー / レーベル / シリーズ",
                nameof(DmmCandidateRow.MakerLabelSeries),
                new DataGridLength(2, DataGridLengthUnitType.Star),
                120));
            CandidatesGrid.Columns.Add(new WpfDataGridTextColumn
            {
                Header = "floor",
                Binding = new Binding(nameof(DmmCandidateRow.FloorLabel)),
                Width = new DataGridLength(80),
                MinWidth = 64,
                IsReadOnly = true,
            });
        }

        private void AddRescueColumns()
        {
            CandidatesGrid.Columns.Add(new WpfDataGridTextColumn
            {
                Header = "CID",
                Binding = new Binding(nameof(DmmJacketGuessRow.Cid)),
                Width = new DataGridLength(140),
                MinWidth = 100,
                IsReadOnly = true,
            });
            CandidatesGrid.Columns.Add(new WpfDataGridTextColumn
            {
                Header = "種別",
                Binding = new Binding(nameof(DmmJacketGuessRow.HostLabel)),
                Width = new DataGridLength(96),
                MinWidth = 80,
                IsReadOnly = true,
            });
            var urlColumn = CreateEllipsisColumn(
                "URL",
                nameof(DmmJacketGuessRow.Url),
                new DataGridLength(1, DataGridLengthUnitType.Star),
                200,
                isReadOnly: false);
            urlColumn.Binding = new Binding(nameof(DmmJacketGuessRow.Url))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            };
            CandidatesGrid.Columns.Add(urlColumn);
        }

        private static WpfDataGridTextColumn CreateEllipsisColumn(
            string header,
            string path,
            DataGridLength width,
            double minWidth,
            bool isReadOnly = true)
        {
            var column = new WpfDataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = width,
                MinWidth = minWidth,
                IsReadOnly = isReadOnly,
            };
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(path)));
            column.ElementStyle = style;
            return column;
        }

        private void CandidatesGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (_listMode != GridListMode.RescueUrls)
            {
                e.Cancel = true;
                return;
            }

            // URL 列以外は編集不可
            if (e.Column is WpfDataGridTextColumn textColumn
                && textColumn.Binding is Binding binding
                && binding.Path?.Path == nameof(DmmJacketGuessRow.Url))
            {
                return;
            }

            e.Cancel = true;
        }

        private async void CandidatesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_listMode != GridListMode.RescueUrls
                || e.EditAction != DataGridEditAction.Commit
                || e.Row?.Item is not DmmJacketGuessRow row)
            {
                return;
            }

            if (e.EditingElement is TextBox edited)
            {
                row.Url = edited.Text?.Trim() ?? string.Empty;
            }

            CandidatesGrid.SelectedItem = row;
            await ShowRescueJacketAsync(row.Url).ConfigureAwait(true);
        }

        private void UpdateModeDependentButtons()
        {
            bool candidates = _listMode == GridListMode.Candidates;
            ApplyButton.IsEnabled = !_isSearching && candidates;
            NextPageButton.IsEnabled = !_isSearching
                && candidates
                && _mayHaveMore
                && !string.IsNullOrWhiteSpace(_lastSearchKeyword);
            AdoptJacketButton.IsEnabled = !_isSearching
                && _listMode == GridListMode.RescueUrls
                && DmmJacketUrls.IsHttpUrl(_rescueJacketUrl);
        }

        private void SetCandidates(IReadOnlyList<DmmCandidateEntry> entries)
        {
            string productCode = ResolveProductCodeForSort();
            _candidates.Clear();
            foreach (DmmCandidateRow row in DmmCandidateRow.FromEntries(entries, productCode))
            {
                _candidates.Add(row);
            }

            SelectPreferredCandidate(productCode);
        }

        private int AppendCandidates(IReadOnlyList<DmmCandidateEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return 0;
            }

            string productCode = ResolveProductCodeForSort();
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DmmCandidateRow row in _candidates)
            {
                existingKeys.Add(CandidateKey(row.ContentId, row.FloorLabel));
            }

            int added = 0;
            foreach (DmmCandidateRow row in DmmCandidateRow.FromEntries(entries, productCode))
            {
                string key = CandidateKey(row.ContentId, row.FloorLabel);
                if (!existingKeys.Add(key))
                {
                    continue;
                }

                _candidates.Add(row);
                added++;
            }

            return added;
        }

        private static string CandidateKey(string contentId, string floorLabel) =>
            $"{contentId ?? string.Empty}\u001f{floorLabel ?? string.Empty}";

        private void SelectPreferredCandidate(string productCode = null)
        {
            productCode ??= ResolveProductCodeForSort();
            DmmCandidateRow preferred = DmmCandidateRow.PreferSelection(_candidates, productCode);
            if (preferred == null)
            {
                return;
            }

            CandidatesGrid.SelectedItem = preferred;
            CandidatesGrid.CurrentItem = preferred;
            CandidatesGrid.ScrollIntoView(preferred);
        }

        private string ResolveProductCodeForSort()
        {
            DmmCidNormalizer.ExtractResult fromKeyword = DmmCidNormalizer.ExtractFromSearchInput(KeywordBox.Text);
            if (fromKeyword.HasProductCode)
            {
                return fromKeyword.ProductCode;
            }

            DmmCidNormalizer.ExtractResult fromName = DmmCidNormalizer.ExtractFromFileName(_record.Movie_Name);
            return fromName.HasProductCode ? fromName.ProductCode : null;
        }

        private DmmMetadataResolveService GetResolver()
        {
            DmmApiOptions options = DmmApiOptions.FromSettings();
            _resolver ??= new DmmMetadataResolveService(new DmmItemListClient(options));
            return _resolver;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await RunSearchAsync(KeywordBox.Text, append: false).ConfigureAwait(true);
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_listMode != GridListMode.Candidates)
            {
                return;
            }

            string keyword = string.IsNullOrWhiteSpace(KeywordBox.Text)
                ? _lastSearchKeyword
                : KeywordBox.Text;
            await RunSearchAsync(keyword, append: true).ConfigureAwait(true);
        }

        private void CandidatesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsOpenClickSuppressed)
            {
                e.Handled = true;
            }
        }

        private void CandidatesGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsOpenClickSuppressed)
            {
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            DataGridCell cell = FindVisualParent<DataGridCell>(source);
            if (cell?.Column == null)
            {
                return;
            }

            if (_listMode == GridListMode.RescueUrls)
            {
                if (cell.DataContext is not DmmJacketGuessRow guess)
                {
                    return;
                }

                // URL 列は編集用。CID クリック時だけ検索語へ反映する
                if (cell.Column is WpfDataGridTextColumn textColumn
                    && textColumn.Binding is Binding binding
                    && binding.Path?.Path == nameof(DmmJacketGuessRow.Url))
                {
                    return;
                }

                string value = GetRescueCellDisplayText(guess, cell.Column);
                if (string.IsNullOrWhiteSpace(value)
                    || string.Equals(value, guess.Url, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                KeywordBox.Text = value.Trim();
                KeywordBox.Focus();
                KeywordBox.CaretIndex = KeywordBox.Text.Length;
                return;
            }

            if (cell.DataContext is not DmmCandidateRow row)
            {
                return;
            }

            // ジャケ列は検索語に載せない。○ ならプレビューへフォーカス。
            if (CandidatesGrid.Columns.Count > 0
                && ReferenceEquals(cell.Column, CandidatesGrid.Columns[0]))
            {
                CandidatesGrid.SelectedItem = row;
                JacketPreviewImage.Focus();
                return;
            }

            string candidateValue = GetCandidateCellDisplayText(row, cell.Column);
            if (string.IsNullOrWhiteSpace(candidateValue))
            {
                return;
            }

            KeywordBox.Text = candidateValue.Trim();
            KeywordBox.Focus();
            KeywordBox.CaretIndex = KeywordBox.Text.Length;
        }

        private async void CandidatesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_listMode == GridListMode.RescueUrls)
            {
                DmmJacketGuessRow guess = GetSelectedRescueRow();
                if (guess != null)
                {
                    await ShowRescueJacketAsync(guess.Url).ConfigureAwait(true);
                }
                else
                {
                    _rescueJacketUrl = null;
                    UpdateModeDependentButtons();
                    ClearJacketPreview();
                }

                return;
            }

            _rescueJacketUrl = null;
            UpdateModeDependentButtons();
            await UpdateJacketPreviewAsync(GetSelectedCandidateRow()).ConfigureAwait(true);
        }

        private static string GetCandidateCellDisplayText(DmmCandidateRow row, DataGridColumn column)
        {
            if (column is not WpfDataGridTextColumn textColumn
                || textColumn.Binding is not Binding binding
                || string.IsNullOrEmpty(binding.Path?.Path))
            {
                return string.Empty;
            }

            return binding.Path.Path switch
            {
                nameof(DmmCandidateRow.Title) => row.Title,
                nameof(DmmCandidateRow.ContentId) => row.ContentId,
                nameof(DmmCandidateRow.MakerLabelSeries) => row.MakerLabelSeries,
                nameof(DmmCandidateRow.FloorLabel) => row.FloorLabel,
                _ => string.Empty,
            };
        }

        private static string GetRescueCellDisplayText(DmmJacketGuessRow row, DataGridColumn column)
        {
            if (column is not WpfDataGridTextColumn textColumn
                || textColumn.Binding is not Binding binding
                || string.IsNullOrEmpty(binding.Path?.Path))
            {
                return string.Empty;
            }

            return binding.Path.Path switch
            {
                nameof(DmmJacketGuessRow.Cid) => row.Cid,
                nameof(DmmJacketGuessRow.HostLabel) => row.HostLabel,
                nameof(DmmJacketGuessRow.Url) => row.Url,
                _ => string.Empty,
            };
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
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

        private async Task RunSearchAsync(string keyword, bool append)
        {
            if (_isSearching)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                StatusText.Text = "検索語を入力してください。";
                return;
            }

            DmmApiOptions options = DmmApiOptions.FromSettings();
            if (!options.IsConfigured)
            {
                StatusText.Text = "DMM API ID / アフィリエイトID（API用）が未設定です。";
                return;
            }

            if (!append)
            {
                ApplyListMode(GridListMode.Candidates, clearRescueSelection: true);
            }
            else if (_listMode != GridListMode.Candidates)
            {
                return;
            }

            string trimmed = keyword.Trim();
            int offset = append ? _nextOffset : 1;
            if (append && !_mayHaveMore)
            {
                StatusText.Text = "これ以上の候補はありません。";
                UpdateNextPageEnabled();
                return;
            }

            _isSearching = true;
            SetSearchEnabled(false);
            StatusText.Text = append ? "追加検索中..." : "検索中...";

            try
            {
                DmmKeywordSearchResult result = await GetResolver()
                    .SearchPageAsync(trimmed, offset, PageHits)
                    .ConfigureAwait(true);

                if (!result.IsConfigured)
                {
                    StatusText.Text = "DMM API が未設定です。";
                    _mayHaveMore = false;
                    UpdateNextPageEnabled();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    StatusText.Text = $"検索エラー: {result.ErrorMessage}";
                    if (!append)
                    {
                        _candidates.Clear();
                        ClearJacketPreview();
                    }

                    _mayHaveMore = false;
                    UpdateNextPageEnabled();
                    return;
                }

                _lastSearchKeyword = trimmed;
                _mayHaveMore = result.MayHaveMore;
                _nextOffset = offset + PageHits;

                if (append)
                {
                    int added = AppendCandidates(result.Candidates);
                    StatusText.Text = added == 0
                        ? $"{_candidates.Count} 件表示中（追加なし）。"
                        : $"{_candidates.Count} 件表示中（+{added}）。";
                }
                else
                {
                    SetCandidates(result.Candidates);
                    StatusText.Text = _candidates.Count == 0
                        ? "候補が見つかりませんでした。品番表記（ハイフン有無・ゼロ埋め等）を変えて再検索してください。"
                        : $"{_candidates.Count} 件の候補が見つかりました。";
                    await UpdateJacketPreviewAsync(GetSelectedCandidateRow()).ConfigureAwait(true);
                }

                UpdateNextPageEnabled();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"検索エラー: {ex.Message}";
                if (!append)
                {
                    _candidates.Clear();
                    ClearJacketPreview();
                }

                _mayHaveMore = false;
                UpdateNextPageEnabled();
            }
            finally
            {
                _isSearching = false;
                SetSearchEnabled(true);
            }
        }

        private void UpdateNextPageEnabled() => UpdateModeDependentButtons();

        private void SetSearchEnabled(bool enabled)
        {
            SearchButton.IsEnabled = enabled;
            KeywordBox.IsEnabled = enabled;
            PlayButton.IsEnabled = enabled;
            TagEditButton.IsEnabled = enabled;
            MetadataEditButton.IsEnabled = enabled;
            JacketRescueButton.IsEnabled = enabled;
            UpdateModeDependentButtons();
            foreach (object child in VariantChipsPanel.Children)
            {
                if (child is Button chip)
                {
                    chip.IsEnabled = enabled;
                }
            }
        }

        private void ClearJacketPreview()
        {
            _previewGeneration++;
            JacketPreviewImage.Source = null;
            JacketPreviewHint.Visibility = Visibility.Visible;
            JacketPreviewHint.Text = _listMode == GridListMode.RescueUrls
                ? "選択行の推定 URL をプレビューします"
                : "選択行のジャケット（pl）を表示します";
        }

        private async Task UpdateJacketPreviewAsync(DmmCandidateRow row)
        {
            int generation = ++_previewGeneration;
            string url = row?.Item?.ImageUrl?.Large?.Trim();
            if (!DmmJacketUrls.IsHttpUrl(url)
                || (Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                    && DmmJacketUrls.IsPlaceholderJacketUri(uri)))
            {
                if (generation == _previewGeneration)
                {
                    JacketPreviewImage.Source = null;
                    JacketPreviewHint.Visibility = Visibility.Visible;
                    JacketPreviewHint.Text = row == null
                        ? "選択行のジャケット（pl）を表示します"
                        : "この候補に表示可能なジャケットがありません";
                }

                return;
            }

            JacketPreviewHint.Text = "読み込み中...";
            JacketPreviewHint.Visibility = Visibility.Visible;

            BitmapSource image = await DmmRemoteImageLoader.LoadAsync(url, Dispatcher).ConfigureAwait(true);
            if (generation != _previewGeneration)
            {
                return;
            }

            if (image == null)
            {
                JacketPreviewImage.Source = null;
                JacketPreviewHint.Text = "ジャケットの読み込みに失敗しました";
                JacketPreviewHint.Visibility = Visibility.Visible;
                return;
            }

            JacketPreviewImage.Source = image;
            JacketPreviewHint.Visibility = Visibility.Collapsed;
        }

        private void JacketPreviewImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 1)
            {
                return;
            }

            ImageSource source = JacketPreviewImage.Source;
            if (source == null)
            {
                return;
            }

            JacketLightboxWindow.Show(this, source);
            e.Handled = true;
        }

        private async void JacketRescueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isSearching)
            {
                return;
            }

            IReadOnlyList<DmmJacketGuessRow> rows = DmmJacketUrlGuess.BuildRowsFromKeyword(KeywordBox.Text);
            ApplyListMode(GridListMode.RescueUrls, clearRescueSelection: true);
            _rescueRows.Clear();
            ClearJacketPreview();

            if (rows.Count == 0)
            {
                StatusText.Text =
                    "ジャケ救済: 検索語を CDN 用 CID として使えません。英数字とアンダースコアのみ（例: h_000abcd00123）を入れて再度実行してください。";
                return;
            }

            foreach (DmmJacketGuessRow row in rows)
            {
                _rescueRows.Add(row);
            }

            StatusText.Text =
                $"ジャケ救済: 推定 URL を {_rescueRows.Count} 件生成しました。行を選んでプレビューし、良ければ「ジャケ採用」してください。";
            CandidatesGrid.SelectedItem = _rescueRows[0];
            CandidatesGrid.ScrollIntoView(_rescueRows[0]);
            await ShowRescueJacketAsync(_rescueRows[0].Url).ConfigureAwait(true);
        }

        private async Task ShowRescueJacketAsync(string url)
        {
            if (!DmmJacketUrls.IsHttpUrl(url))
            {
                _rescueJacketUrl = null;
                UpdateModeDependentButtons();
                return;
            }

            _rescueJacketUrl = url.Trim();
            UpdateModeDependentButtons();

            int generation = ++_previewGeneration;
            JacketPreviewHint.Text = "読み込み中...";
            JacketPreviewHint.Visibility = Visibility.Visible;

            BitmapSource image = await DmmRemoteImageLoader.LoadAsync(_rescueJacketUrl, Dispatcher)
                .ConfigureAwait(true);
            if (generation != _previewGeneration)
            {
                return;
            }

            if (image == null)
            {
                JacketPreviewImage.Source = null;
                JacketPreviewHint.Text = "画像を取得できませんでした（未存在・now_printing 等）";
                JacketPreviewHint.Visibility = Visibility.Visible;
                return;
            }

            if (Uri.TryCreate(_rescueJacketUrl, UriKind.Absolute, out Uri checkUri)
                && DmmJacketUrls.IsPlaceholderJacketUri(checkUri))
            {
                JacketPreviewImage.Source = image;
                JacketPreviewHint.Text = "プレースホルダ画像の可能性があります";
                JacketPreviewHint.Visibility = Visibility.Visible;
                return;
            }

            JacketPreviewImage.Source = image;
            JacketPreviewHint.Visibility = Visibility.Collapsed;
        }

        private void AdoptJacketButton_Click(object sender, RoutedEventArgs e)
        {
            if (_listMode != GridListMode.RescueUrls || !DmmJacketUrls.IsHttpUrl(_rescueJacketUrl))
            {
                StatusText.Text = "採用する推定ジャケがありません。";
                return;
            }

            string url = _rescueJacketUrl.Trim();
            _record.Comment1 = url;
            if (!string.IsNullOrWhiteSpace(_dbPath))
            {
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Comment1, url);
            }

            StatusText.Text = "推定ジャケ URL を Comment1 に保存しました。";
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_record.Movie_Path) || !Path.Exists(_record.Movie_Path))
            {
                StatusText.Text = "再生対象のファイルが見つかりません。";
                return;
            }

            var dbSettings = string.IsNullOrWhiteSpace(_dbPath)
                ? null
                : new DatabaseSettings(_dbPath);
            string moviePathQuoted = $"\"{_record.Movie_Path}\"";
            ExternalPlayerLaunchRequest request = ExternalPlayerLauncher.BuildRequest(
                dbSettings?.PlayerPrg,
                dbSettings?.PlayerParam,
                Properties.Settings.Default.DefaultPlayerPath,
                Properties.Settings.Default.DefaultPlayerParam,
                _record,
                moviePathQuoted,
                0);

            try
            {
                await ExternalPlayerLauncher.LaunchAsync(request, this).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"再生エラー: {ex.Message}";
            }
        }

        private void TagEditButton_Click(object sender, RoutedEventArgs e)
        {
            var tagEditWindow = new TagEdit
            {
                Title = "タグ編集",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DataContext = _record,
            };
            tagEditWindow.ShowDialog();

            if (tagEditWindow.CloseStatus() == MessageBoxResult.Cancel)
            {
                return;
            }

            if (tagEditWindow.DataContext is not MovieRecords dc)
            {
                return;
            }

            TagMutationService.ApplyEdit(_record, dc.Tags);
            if (!string.IsNullOrWhiteSpace(_dbPath))
            {
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, "tag", _record.Tags);
            }

            StatusText.Text = "タグを更新しました。";
        }

        private void MetadataEditButton_Click(object sender, RoutedEventArgs e)
        {
            var editModel = MetadataEditModel.FromMovie(_record);
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

            editModel.ApplyTo(_record);
            if (!string.IsNullOrWhiteSpace(_dbPath))
            {
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Title, _record.Title);
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Comment1, _record.Comment1);
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Comment2, _record.Comment2);
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Comment3, _record.Comment3);
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Artist, _record.Artist);
                SQLite.UpdateMovieSingleColumn(_dbPath, _record.Movie_Id, MovieColumn.Genre, _record.Genre);
            }

            StatusText.Text = "メタ情報を更新しました。";
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_listMode != GridListMode.Candidates)
            {
                StatusText.Text = "候補一覧に戻してから適用してください（検索ボタン）。";
                return;
            }

            DmmCandidateRow selected = GetSelectedCandidateRow();
            if (selected?.Item == null)
            {
                StatusText.Text = "適用する候補を選択してください。";
                return;
            }

            try
            {
                _applier.Apply(_dbPath, _record, selected.Item, action => action(), manualOverwrite: true);
                if (_pendingId.HasValue && !string.IsNullOrEmpty(_dbPath))
                {
                    DmmPendingCandidateStore.Delete(_dbPath, _pendingId.Value);
                }

                AppliedSuccessfully = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"適用エラー: {ex.Message}";
            }
        }

        private DmmCandidateRow GetSelectedCandidateRow()
        {
            if (CandidatesGrid.SelectedItem is DmmCandidateRow row)
            {
                return row;
            }

            if (CandidatesGrid.CurrentItem is DmmCandidateRow current)
            {
                return current;
            }

            if (CandidatesGrid.SelectedCells.Count > 0
                && CandidatesGrid.SelectedCells[0].Item is DmmCandidateRow cellRow)
            {
                return cellRow;
            }

            return null;
        }

        private DmmJacketGuessRow GetSelectedRescueRow()
        {
            if (CandidatesGrid.SelectedItem is DmmJacketGuessRow row)
            {
                return row;
            }

            if (CandidatesGrid.CurrentItem is DmmJacketGuessRow current)
            {
                return current;
            }

            if (CandidatesGrid.SelectedCells.Count > 0
                && CandidatesGrid.SelectedCells[0].Item is DmmJacketGuessRow cellRow)
            {
                return cellRow;
            }

            return null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
