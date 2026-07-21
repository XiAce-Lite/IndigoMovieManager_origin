using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// DMM 情報取得の進捗表示（ステータスバー・キャンセル対応）。
    /// </summary>
    internal sealed class DmmFetchProgressSession : IDisposable
    {
        private readonly StatusBarProgressCoordinator.FileInfoSlotHandle _handle;
        private readonly int _total;

        public DmmFetchProgressSession(int total)
        {
            _total = total;
            _handle = StatusBarProgressHost.Coordinator.BeginFileInfo(total, "DMM情報取得中");
            Report(0, "開始しています…");
        }

        public CancellationToken Cancel => _handle.Cancel;

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

            string message = string.IsNullOrEmpty(detail)
                ? string.Empty
                : ProgressPathFormatter.Format(detail, StatusBarProgressViewModel.DetailMaxWidth);

            _handle.Report(clampedDone, message);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
