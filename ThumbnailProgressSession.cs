using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// サムネイル作成の進捗表示（ステータスバー）。
    /// </summary>
    internal sealed class ThumbnailProgressSession : IDisposable
    {
        internal const double MessageFontSize = StatusBarProgressViewModel.MessageFontSize;

        private readonly string _baseTitle;
        private readonly int _jobSwitchToken;
        private StatusBarProgressCoordinator.ThumbnailSlotHandle _handle;
        private bool _disposed;

        public ThumbnailProgressSession(string primaryLayoutKey, string displayTitle, int jobSwitchToken)
        {
            _baseTitle = ResolveBaseTitle(primaryLayoutKey, displayTitle);
            _jobSwitchToken = jobSwitchToken;
        }

        public ThumbnailProgressSession(string primaryLayoutKey, int jobSwitchToken)
            : this(primaryLayoutKey, null, jobSwitchToken)
        {
        }

        public ThumbnailProgressSession(int primaryTabIndex, int jobSwitchToken)
            : this($"legacy-tab:{primaryTabIndex}", null, jobSwitchToken)
        {
        }

        public bool IsVisible => _handle != null;

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
            if (_disposed || _handle == null)
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

            string message = detail ?? string.Empty;

            _handle.Report(title, percent, message);
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ThumbnailProgressRegistry.Unregister(this);

            if (_handle != null)
            {
                _handle.Dispose();
                _handle = null;
            }
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
            if (_disposed || _handle != null)
            {
                return;
            }

            _handle = StatusBarProgressHost.Coordinator.BeginThumbnail(_baseTitle);
            ThumbnailProgressRegistry.Register(this);
        }

        private static string ResolveBaseTitle(string layoutKey, string displayTitle)
        {
            if (!string.IsNullOrWhiteSpace(displayTitle))
            {
                return $"サムネイル作成中 ({displayTitle})";
            }

            return GetBaseTitle(layoutKey);
        }

        private static string GetBaseTitle(string layoutKey)
        {
            if (string.IsNullOrEmpty(layoutKey))
            {
                return "サムネイル作成中";
            }

            if (layoutKey.StartsWith("legacy-tab:", StringComparison.Ordinal))
            {
                return GetLegacyTabTitle(layoutKey["legacy-tab:".Length..]);
            }

            return "サムネイル作成中";
        }

        private static string GetLegacyTabTitle(string tabPart) =>
            int.TryParse(tabPart, out int tabIndex)
                ? tabIndex switch
                {
                    0 => "サムネイル作成中(Small)",
                    1 => "サムネイル作成中(Big)",
                    2 => "サムネイル作成中(Grid)",
                    3 => "サムネイル作成中(List)",
                    4 => "サムネイル作成中(Big10)",
                    _ => "サムネイル作成中",
                }
                : "サムネイル作成中";
    }
}
