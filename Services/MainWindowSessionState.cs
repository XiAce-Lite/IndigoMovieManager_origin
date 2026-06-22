namespace IndigoMovieManager.Services
{
    /// <summary>
    /// MainWindow の DB セッションとフィルタ世代を管理する。
    /// </summary>
    internal sealed class MainWindowSessionState
    {
        private int _filterGeneration;

        public string ActiveDbFullPath { get; private set; } = "";

        public void SetActiveDb(string dbFullPath)
        {
            ActiveDbFullPath = dbFullPath ?? "";
            BumpFilterGeneration();
        }

        public int BumpFilterGeneration() => Interlocked.Increment(ref _filterGeneration);

        public int FilterGeneration => Volatile.Read(ref _filterGeneration);

        public bool IsActiveDb(string dbFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath))
            {
                return false;
            }

            return string.Equals(ActiveDbFullPath, dbFullPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
