using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// 件数ベース進捗の共通処理。表示文言やスロット種別は呼び出し側で決める。
    /// </summary>
    internal sealed class CountedProgressSession : IDisposable
    {
        private readonly Action<int, string> _report;
        private readonly Action _dispose;
        private readonly Func<string, string> _detailFormatter;
        private readonly int _total;

        public CountedProgressSession(
            int total,
            Action<int, string> report,
            Action dispose,
            CancellationToken cancel = default,
            string initialDetail = "",
            Func<string, string> detailFormatter = null)
        {
            _total = total;
            _report = report ?? throw new ArgumentNullException(nameof(report));
            _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
            _detailFormatter = detailFormatter;
            Cancel = cancel;
            Report(0, initialDetail);
        }

        public CancellationToken Cancel { get; }

        public void Report(int done, string detail)
        {
            int clampedDone = Math.Clamp(done, 0, _total);
            string message = _detailFormatter == null
                ? detail
                : _detailFormatter(detail);

            _report(clampedDone, message);
        }

        public void Dispose()
        {
            _dispose();
        }

        public static string FormatDetail(string detail)
        {
            return string.IsNullOrEmpty(detail)
                ? string.Empty
                : ProgressPathFormatter.Format(detail, StatusBarProgressViewModel.DetailMaxWidth);
        }

        public static CountedProgressSession BeginFileInfo(int total, string statusLabel = null)
        {
            StatusBarProgressCoordinator.FileInfoSlotHandle handle =
                StatusBarProgressHost.Coordinator.BeginFileInfo(total, statusLabel);
            return new CountedProgressSession(
                total,
                handle.Report,
                handle.Dispose,
                handle.Cancel,
                "開始しています…",
                FormatDetail);
        }

        public static CountedProgressSession BeginDmmFetch(int total) =>
            BeginFileInfo(total, "DMM情報取得中");
    }
}
