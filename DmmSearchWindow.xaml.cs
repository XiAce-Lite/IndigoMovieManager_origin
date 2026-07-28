using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;

namespace IndigoMovieManager
{
    public sealed class DmmSearchWindowModel
    {
        public string TargetFileLabel { get; set; }
    }

    /// <summary>
    /// DMM 候補の検索・選択ダイアログ。
    /// </summary>
    public partial class DmmSearchWindow : Window
    {
        /// <summary>手動検索の1ページ件数（0620c02 以降の候補30件方針に合わせる）。</summary>
        private const int PageHits = 30;

        private readonly MovieRecords _record;
        private readonly string _dbPath;
        private readonly long? _pendingId;
        private readonly ObservableCollection<DmmCandidateRow> _candidates = [];
        private readonly DmmMetadataApplyService _applier = new();
        private DmmMetadataResolveService _resolver;
        private bool _isSearching;
        /// <summary>親画面のダブルクリック MouseUp がセルクリックに化けるのを抑止する。</summary>
        private DateTime _suppressCellClickUntilUtc;
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

            DataContext = new DmmSearchWindowModel
            {
                TargetFileLabel = $"対象: {record.Movie_Name ?? record.Movie_Path ?? "(不明)"}",
            };

            KeywordBox.Text = initialKeyword ?? string.Empty;
            CandidatesGrid.ItemsSource = _candidates;
            PopulateVariantChips();

            if (initialCandidates is { Count: > 0 })
            {
                SetCandidates(initialCandidates);
                StatusText.Text = $"{_candidates.Count} 件の候補を表示しています。";
                // 未確定一覧からのダブルクリック直後に開く場合の誤クリック吸収
                _suppressCellClickUntilUtc = DateTime.UtcNow.AddMilliseconds(400);
                _lastSearchKeyword = (initialKeyword ?? string.Empty).Trim();
                // 既存候補のあとの「次の30件」は offset=1 から取得し、重複は追記側で除外する。
                _nextOffset = 1;
                _mayHaveMore = !string.IsNullOrWhiteSpace(_lastSearchKeyword);
                UpdateNextPageEnabled();
            }

            Loaded += DmmSearchWindow_Loaded;
        }

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
            if (_candidates.Count > 0)
            {
                SelectPreferredCandidate();
                await UpdateJacketPreviewAsync(GetSelectedCandidateRow()).ConfigureAwait(true);
                return;
            }

            await RunSearchAsync(KeywordBox.Text, append: false).ConfigureAwait(true);
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
            DmmCidNormalizer.ExtractResult fromKeyword = DmmCidNormalizer.ExtractFromFileName(KeywordBox.Text);
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
            string keyword = string.IsNullOrWhiteSpace(KeywordBox.Text)
                ? _lastSearchKeyword
                : KeywordBox.Text;
            await RunSearchAsync(keyword, append: true).ConfigureAwait(true);
        }

        private void CandidatesGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DateTime.UtcNow < _suppressCellClickUntilUtc)
            {
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            DataGridCell cell = FindVisualParent<DataGridCell>(source);
            if (cell?.Column == null || cell.DataContext is not DmmCandidateRow row)
            {
                return;
            }

            // ジャケ列は検索語に載せない。○ ならプレビューへフォーカス。
            if (ReferenceEquals(cell.Column, CandidatesGrid.Columns[0]))
            {
                CandidatesGrid.SelectedItem = row;
                JacketPreviewImage.Focus();
                return;
            }

            string value = GetCellDisplayText(row, cell.Column);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            KeywordBox.Text = value.Trim();
            KeywordBox.Focus();
            KeywordBox.CaretIndex = KeywordBox.Text.Length;
        }

        private async void CandidatesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateJacketPreviewAsync(GetSelectedCandidateRow()).ConfigureAwait(true);
        }

        private static string GetCellDisplayText(DmmCandidateRow row, DataGridColumn column)
        {
            if (column is not DataGridTextColumn textColumn
                || textColumn.Binding is not System.Windows.Data.Binding binding
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

        private void UpdateNextPageEnabled()
        {
            NextPageButton.IsEnabled = !_isSearching && _mayHaveMore && !string.IsNullOrWhiteSpace(_lastSearchKeyword);
        }

        private void SetSearchEnabled(bool enabled)
        {
            SearchButton.IsEnabled = enabled;
            ApplyButton.IsEnabled = enabled;
            KeywordBox.IsEnabled = enabled;
            UpdateNextPageEnabled();
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
            JacketPreviewHint.Text = "選択行のジャケット（pl）を表示します";
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

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
