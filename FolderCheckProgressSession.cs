using Notification.Wpf;

namespace IndigoMovieManager
{
    /// <summary>
    /// フォルダ監視の進捗ポップアップ（NotificationManager の寿命を保持する）。
    /// </summary>
    internal sealed class FolderCheckProgressSession : IDisposable
    {
        private readonly NotificationManager _notificationManager = new();
        private readonly IProgress<(double? progress, string message, string title, bool? showCancel)> _progress;
        private readonly int _total;

        public FolderCheckProgressSession(int totalFolders)
        {
            _total = totalFolders;
            _progress = _notificationManager.ShowProgressBar(
                "フォルダ監視中",
                true,
                false,
                "ProgressArea",
                false,
                2,
                "");
            Report(0, "監視を開始しています…");
        }

        public void Report(int done, string detail)
        {
            int clampedDone = done;
            if (clampedDone < 0)
            {
                clampedDone = 0;
            }

            if (clampedDone > _total)
            {
                clampedDone = _total;
            }

            int percent = _total > 0
                ? (int)Math.Round((double)clampedDone * 100d / _total)
                : 100;
            if (percent > 100)
            {
                percent = 100;
            }

            string message = _total > 1
                ? $"({clampedDone}/{_total}) {detail}"
                : detail;

            _progress.Report((percent, message, "フォルダ監視中", false));
        }

        public void Complete()
        {
            Report(_total, "監視完了");
            Dispose();
        }

        public void Dispose()
        {
            if (_progress is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
