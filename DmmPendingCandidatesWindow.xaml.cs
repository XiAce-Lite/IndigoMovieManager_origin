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
        private readonly ObservableCollection<DmmPendingRow> _rows = [];

        internal DmmPendingCandidatesWindow(
            string dbPath,
            Func<long, MovieRecords> findMovie,
            Action onResolved = null)
        {
            InitializeComponent();

            _dbPath = dbPath ?? string.Empty;
            _findMovie = findMovie ?? (_ => null);
            _onResolved = onResolved;

            PendingGrid.ItemsSource = _rows;
            Reload();
        }

        private void Reload()
        {
            _rows.Clear();
            if (string.IsNullOrEmpty(_dbPath))
            {
                return;
            }

            foreach (DmmPendingCandidateRecord record in DmmPendingCandidateStore.List(_dbPath))
            {
                _rows.Add(DmmPendingRow.FromRecord(record));
            }
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

            OpenResolveWindow(row);
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
                new Action(() => OpenResolveWindow(row)),
                DispatcherPriority.ApplicationIdle);
        }

        private void OpenResolveWindow(DmmPendingRow row)
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
