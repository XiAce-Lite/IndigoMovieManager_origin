using System.Collections.ObjectModel;
using System.Windows;
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

        private void ResolveButton_Click(object sender, RoutedEventArgs e)
        {
            DmmPendingRow row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show(this, "保留レコードを選択してください。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
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
            DmmPendingRow row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show(this, "破棄する保留レコードを選択してください。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show(
                this,
                $"「{row.MovieName}」の未確定候補を破棄します。よろしいですか？",
                Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            DmmPendingCandidateStore.Delete(_dbPath, row.PendingId);
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
    }
}
