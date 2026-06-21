using Notification.Wpf;
using Notification.Wpf.Base;
using Notification.Wpf.Classes;
using Notification.Wpf.Constants;

namespace IndigoMovieManager
{
    /// <summary>
    /// ファイル情報再取得の進捗ポップアップ（キャンセル対応）。
    /// </summary>
    internal sealed class FileInfoProgressSession : IDisposable
    {
        internal const double MessageFontSize = 12d;
        internal const double MessageAreaWidth = 268d;

        private readonly NotificationManager _notificationManager = new();
        private readonly NotifierProgress<(double? progress, string message, string title, bool? showCancel)> _progress;
        private readonly int _total;

        static FileInfoProgressSession()
        {
            NotificationConstants.MaxWidth = ThumbnailProgressSession.PopupWidth;
            NotificationConstants.MinWidth = ThumbnailProgressSession.PopupWidth;
        }

        public FileInfoProgressSession(int total)
        {
            _total = total;
            _progress = _notificationManager.ShowProgressBar(
                "ファイル情報再取得中",
                true,
                false,
                "ProgressArea",
                true,
                1,
                "",
                false,
                false,
                null,
                null,
                null,
                null,
                null,
                new TextContentSettings
                {
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = MessageFontSize,
                });
            Report(0, "開始しています…");
        }

        public CancellationToken Cancel => _progress.Cancel;

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

            string title = _total > 0
                ? $"ファイル情報再取得中 ({clampedDone}/{_total})"
                : "ファイル情報再取得中";

            string message = string.IsNullOrEmpty(detail)
                ? string.Empty
                : ProgressPathFormatter.Format(detail, MessageAreaWidth);

            _progress.Report((percent, message, title, true));
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
