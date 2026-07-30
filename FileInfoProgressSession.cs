using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// ファイル情報再取得の進捗表示（ステータスバー・キャンセル対応）。
    /// </summary>
    internal sealed class FileInfoProgressSession : IDisposable
    {
        private readonly CountedProgressSession _session;

        public FileInfoProgressSession(int total)
        {
            StatusBarProgressCoordinator.FileInfoSlotHandle handle =
                StatusBarProgressHost.Coordinator.BeginFileInfo(total);
            _session = new CountedProgressSession(
                total,
                handle.Report,
                handle.Dispose,
                handle.Cancel,
                "開始しています…",
                CountedProgressSession.FormatDetail);
        }

        public CancellationToken Cancel => _session.Cancel;

        public void Report(int done, string detail)
        {
            _session.Report(done, detail);
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
