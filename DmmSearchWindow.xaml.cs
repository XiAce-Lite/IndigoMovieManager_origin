using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
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
                return;
            }

            await RunSearchAsync(KeywordBox.Text).ConfigureAwait(true);
        }

        private void SetCandidates(IReadOnlyList<DmmCandidateEntry> entries)
        {
            _candidates.Clear();
            foreach (DmmCandidateRow row in DmmCandidateRow.FromEntries(entries))
            {
                _candidates.Add(row);
            }
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

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (CandidatesGrid.SelectedItem is not DmmCandidateRow selected || selected.Item == null)
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
