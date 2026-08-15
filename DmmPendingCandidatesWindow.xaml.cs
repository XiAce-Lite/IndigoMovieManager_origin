using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IndigoMovieManager.Services.Dmm;

namespace IndigoMovieManager
{
    internal sealed class DmmPendingRow
    {
        public long PendingId { get; init; }
        public long MovieId { get; init; }
        public string MovieName { get; init; }
        public string InitialKeyword { get; init; }
        public int CandidateCount { get; init; }
        public string Source { get; init; }
        public string SourceLabel => Source switch
        {
            "bulk" => "一括",
            "auto" => "自動",
            _ => Source ?? string.Empty,
        };
        public DateTime CreatedAt { get; init; }
        public string CreatedAtLabel => CreatedAt.ToString("yyyy/MM/dd HH:mm");
        public IReadOnlyList<DmmCandidateEntry> Candidates { get; init; } = [];

        public static DmmPendingRow FromRecord(DmmPendingCandidateRecord record) =>
            new()
            {
                PendingId = record.PendingId,
                MovieId = record.MovieId,
                MovieName = record.MovieName,
                InitialKeyword = record.InitialKeyword,
                CandidateCount = record.Candidates?.Count ?? 0,
                Source = record.Source,
                CreatedAt = record.CreatedAt,
                Candidates = record.Candidates ?? [],
            };
    }

    /// <summary>
    /// DMM 未確定候補の一覧と解決。
    /// </summary>
    public partial class DmmPendingCandidatesWindow : Window
    {
        private readonly string _dbPath;
        private readonly Func<long, MovieRecords> _findMovie;
        private readonly Action _onResolved;
        private readonly Func<MovieRecords, Task> _showInSkinAsync;
        private readonly List<DmmPendingRow> _allRows = [];
        private readonly ObservableCollection<DmmPendingRow> _rows = [];
        private bool _imeComposing;

        internal DmmPendingCandidatesWindow(
            string dbPath,
            Func<long, MovieRecords> findMovie,
            Action onResolved = null,
            Func<MovieRecords, Task> showInSkinAsync = null)
        {
            InitializeComponent();

            _dbPath = dbPath ?? string.Empty;
            _findMovie = findMovie ?? (_ => null);
            _onResolved = onResolved;
            _showInSkinAsync = showInSkinAsync;

            TextCompositionManager.AddPreviewTextInputHandler(FilterBox, OnFilterPreviewTextInput);
            TextCompositionManager.AddPreviewTextInputStartHandler(FilterBox, OnFilterPreviewTextInputStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(FilterBox, OnFilterPreviewTextInputUpdate);

            PendingGrid.ItemsSource = _rows;
            ShowInSkinCheckBox.IsChecked = Properties.Settings.Default.DmmPendingShowInSkin;
            Reload();
        }

        private void Reload()
        {
            _allRows.Clear();
            if (!string.IsNullOrEmpty(_dbPath))
            {
                foreach (DmmPendingCandidateRecord record in DmmPendingCandidateStore.List(_dbPath))
                {
                    _allRows.Add(DmmPendingRow.FromRecord(record));
                }
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = FilterBox?.Text ?? "";
            _rows.Clear();
            foreach (DmmPendingRow row in _allRows)
            {
                if (DmmPendingFileNameFilter.Matches(row.MovieName, query))
                {
                    _rows.Add(row);
                }
            }

            UpdateFilterCount();
        }

        private void UpdateFilterCount()
        {
            if (FilterCountText == null)
            {
                return;
            }

            if (_allRows.Count == 0)
            {
                FilterCountText.Text = "0 件";
                return;
            }

            FilterCountText.Text = DmmPendingFileNameFilter.IsBroadQuery(FilterBox?.Text)
                ? $"{_allRows.Count} 件"
                : $"{_rows.Count} / {_allRows.Count} 件";
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_imeComposing)
            {
                return;
            }

            ApplyFilter();
        }

        private void OnFilterPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            _imeComposing = false;
            ApplyFilter();
        }

        private void OnFilterPreviewTextInputStart(object sender, TextCompositionEventArgs e)
        {
            _imeComposing = true;
        }

        private void OnFilterPreviewTextInputUpdate(object sender, TextCompositionEventArgs e)
        {
            if (e.TextComposition.CompositionText.Length == 0)
            {
                _imeComposing = false;
                ApplyFilter();
            }
        }

        private void ShowInSkinCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DmmPendingShowInSkin = ShowInSkinCheckBox.IsChecked == true;
            Properties.Settings.Default.Save();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            e.Handled = true;
            FilterBox.Focus();
            FilterBox.SelectAll();
        }

        private DmmPendingRow GetSelectedRow()
        {
            return PendingGrid.SelectedItem as DmmPendingRow;
        }

        private List<DmmPendingRow> GetSelectedRows()
        {
            var selected = new List<DmmPendingRow>();
            foreach (object item in PendingGrid.SelectedItems)
            {
                if (item is DmmPendingRow row)
                {
                    selected.Add(row);
                }
            }

            return selected;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            PendingGrid.Focus();
            PendingGrid.SelectAll();
        }

        private void ResolveButton_Click(object sender, RoutedEventArgs e)
        {
            DmmPendingRow row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show(this, "保留レコードを選択してください。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _ = OpenResolveWindowAsync(row);
        }

        private void PendingGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // ダブルクリック第2撃の MouseUp が直後の候補画面へ届くのを防ぐ
            if (e.ClickCount >= 2)
            {
                e.Handled = true;
            }
        }

        private void PendingGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not DmmPendingRow row)
            {
                return;
            }

            e.Handled = true;
            Dispatcher.BeginInvoke(
                new Action(() => _ = OpenResolveWindowAsync(row)),
                DispatcherPriority.ApplicationIdle);
        }

        private async Task OpenResolveWindowAsync(DmmPendingRow row)
        {
            if (row == null)
            {
                return;
            }

            MovieRecords record = _findMovie(row.MovieId);
            if (record == null)
            {
                MessageBox.Show(
                    this,
                    "対象レコードが見つかりません。一覧から破棄するか、DB を開き直してください。",
                    Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (ShowInSkinCheckBox.IsChecked == true && _showInSkinAsync != null)
            {
                try
                {
                    await _showInSkinAsync(record).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        $"スキン表示に失敗しました。\n{ex.Message}",
                        Title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            var searchWindow = new DmmSearchWindow(
                record,
                _dbPath,
                row.InitialKeyword,
                row.Candidates,
                row.PendingId)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (searchWindow.ShowDialog() == true && searchWindow.AppliedSuccessfully)
            {
                Reload();
                _onResolved?.Invoke();
            }
        }

        private void DiscardButton_Click(object sender, RoutedEventArgs e)
        {
            List<DmmPendingRow> selected = GetSelectedRows();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "破棄する保留レコードを選択してください。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMessage = selected.Count == 1
                ? $"「{selected[0].MovieName}」の未確定候補を破棄します。よろしいですか？"
                : $"選択中の未確定候補 {selected.Count} 件を破棄します。よろしいですか？";

            MessageBoxResult confirm = MessageBox.Show(
                this,
                confirmMessage,
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            DmmPendingCandidateStore.DeleteMany(_dbPath, selected.Select(row => row.PendingId));
            Reload();
            _onResolved?.Invoke();
        }

        private void CleanupOrphansButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_dbPath))
            {
                return;
            }

            int removed = DmmPendingCandidateStore.DeleteOrphaned(_dbPath);
            Reload();
            _onResolved?.Invoke();
            MessageBox.Show(
                this,
                removed > 0
                    ? $"登録から削除済みの未確定候補を {removed} 件削除しました。"
                    : "削除対象の孤児候補はありませんでした。",
                Title,
                MessageBoxButton.OK,
                removed > 0 ? MessageBoxImage.Information : MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
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
    }
}
