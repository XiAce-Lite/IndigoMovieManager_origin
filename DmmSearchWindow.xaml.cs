using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        private readonly MovieRecords _record;
        private readonly string _dbPath;
        private readonly long? _pendingId;
        private readonly ObservableCollection<DmmCandidateRow> _candidates = [];
        private readonly DmmMetadataApplyService _applier = new();
        private DmmMetadataResolveService _resolver;
        private bool _isSearching;
        /// <summary>親画面のダブルクリック MouseUp がセルクリックに化けるのを抑止する。</summary>
        private DateTime _suppressCellClickUntilUtc;

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
                await RunSearchAsync(variant).ConfigureAwait(true);
            }
        }

        private async void DmmSearchWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_candidates.Count > 0)
            {
                SelectPreferredCandidate();
                return;
            }

            await RunSearchAsync(KeywordBox.Text).ConfigureAwait(true);
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
            await RunSearchAsync(KeywordBox.Text).ConfigureAwait(true);
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

            // ジャケ列は検索語に載せない。
            if (ReferenceEquals(cell.Column, CandidatesGrid.Columns[0]))
            {
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

        private async Task RunSearchAsync(string keyword)
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

            _isSearching = true;
            SetSearchEnabled(false);
            StatusText.Text = "検索中...";

            try
            {
                DmmKeywordSearchResult result = await GetResolver()
                    .SearchManualAsync(keyword.Trim())
                    .ConfigureAwait(true);

                if (!result.IsConfigured)
                {
                    StatusText.Text = "DMM API が未設定です。";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    StatusText.Text = $"検索エラー: {result.ErrorMessage}";
                    _candidates.Clear();
                    return;
                }

                SetCandidates(result.Candidates);
                StatusText.Text = _candidates.Count == 0
                    ? "候補が見つかりませんでした。品番表記（ハイフン有無・ゼロ埋め等）を変えて再検索してください。"
                    : $"{_candidates.Count} 件の候補が見つかりました。";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"検索エラー: {ex.Message}";
                _candidates.Clear();
            }
            finally
            {
                _isSearching = false;
                SetSearchEnabled(true);
            }
        }

        private void SetSearchEnabled(bool enabled)
        {
            SearchButton.IsEnabled = enabled;
            ApplyButton.IsEnabled = enabled;
            KeywordBox.IsEnabled = enabled;
            foreach (object child in VariantChipsPanel.Children)
            {
                if (child is Button chip)
                {
                    chip.IsEnabled = enabled;
                }
            }
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
