using Notification.Wpf;
using Notification.Wpf.Base;
using Notification.Wpf.Constants;

namespace IndigoMovieManager
{
    /// <summary>
    /// サムネイル作成の進捗ポップアップ（NotificationManager の寿命を保持する）。
    /// </summary>
    internal sealed class ThumbnailProgressSession : IDisposable
    {
        internal const double MessageFontSize = 12d;
        internal const double PopupWidth = 400d;
        internal const double MessageAreaWidth = 268d;

        private readonly NotificationManager _notificationManager = new();
        private readonly string _baseTitle;
        private readonly int _jobSwitchToken;
        private readonly TextContentSettings _messageSettings = new()
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = MessageFontSize,
        };

        private IProgress<(double? progress, string message, string title, bool? showCancel)> _progress;
        private bool _disposed;

        static ThumbnailProgressSession()
        {
            NotificationConstants.MaxWidth = PopupWidth;
            NotificationConstants.MinWidth = PopupWidth;
        }

        public ThumbnailProgressSession(int primaryTabIndex, int jobSwitchToken)
        {
            _baseTitle = GetBaseTitle(primaryTabIndex);
            _jobSwitchToken = jobSwitchToken;
        }

        public bool IsVisible => _progress != null;

        public bool TryReport(
            ThumbnailJobCoordinator coordinator,
            int jobId,
            int completed,
            int total,
            string detail)
        {
            if (_disposed || coordinator == null || !CanReport(coordinator, jobId))
            {
                return false;
            }

            EnsureShown();
            if (_disposed || _progress == null)
            {
                return false;
            }

            int clampedCompleted = completed;
            if (clampedCompleted < 0)
            {
                clampedCompleted = 0;
            }

            if (total > 0 && clampedCompleted > total)
            {
                clampedCompleted = total;
            }

            int percent = total > 0
                ? (int)Math.Round((double)clampedCompleted * 100d / total)
                : 0;
            if (percent > 100)
            {
                percent = 100;
            }

            string title = total > 0
                ? $"{_baseTitle} ({clampedCompleted}/{total})"
                : _baseTitle;

            string message = string.IsNullOrEmpty(detail)
                ? string.Empty
                : ProgressPathFormatter.Format(detail, MessageAreaWidth);

            _progress.Report((percent, message, title, false));
            return true;
        }

        public void Dispose()
        {
            _disposed = true;

            if (_progress is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _progress = null;
        }

        private bool CanReport(ThumbnailJobCoordinator coordinator, int jobId)
        {
            if (_jobSwitchToken != coordinator.JobSwitchToken)
            {
                return false;
            }

            if (jobId != coordinator.CurrentJobId)
            {
                return false;
            }

            ThumbnailJobCoordinator.Snapshot snapshot = coordinator.GetSnapshot(jobId);
            return snapshot.JobId == jobId
                && snapshot.Total > 0
                && !snapshot.Abandoned;
        }

        private void EnsureShown()
        {
            if (_disposed || _progress != null)
            {
                return;
            }

            _progress = _notificationManager.ShowProgressBar(
                _baseTitle,
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
                _messageSettings);
        }

        private static string GetBaseTitle(int tabIndex)
        {
            return tabIndex switch
            {
                0 => "サムネイル作成中(Small)",
                1 => "サムネイル作成中(Big)",
                2 => "サムネイル作成中(Grid)",
                3 => "サムネイル作成中(List)",
                4 => "サムネイル作成中(Big10)",
                _ => "サムネイル作成中",
            };
        }
    }
}
