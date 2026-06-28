using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    /// <summary>
    /// フォルダ監視の進捗表示（ステータスバー）。
    /// </summary>
    internal sealed class FolderCheckProgressSession : IDisposable
    {
        private readonly StatusBarProgressCoordinator.FolderCheckSlotHandle _handle;
        private readonly int _total;

        public FolderCheckProgressSession(int totalFolders)
        {
            _total = totalFolders;
            _handle = StatusBarProgressHost.Coordinator.BeginFolderCheck(totalFolders);
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

            _handle.Report(clampedDone, detail);
        }

        public void Complete()
        {
            Report(_total, "監視完了");
            _handle.Dispose();
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
