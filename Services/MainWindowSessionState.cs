namespace IndigoMovieManager.Services
{
    /// <summary>
    /// MainWindow の DB セッションとフィルタ世代を管理する。
    /// </summary>
    internal sealed class MainWindowSessionState
    {
        private int _filterGeneration;
        private int _folderCheckGeneration;
        private int _thumbnailWorkGeneration;

        public string ActiveDbFullPath { get; private set; } = "";

        public void SetActiveDb(string dbFullPath)
        {
            ActiveDbFullPath = dbFullPath ?? "";
            BumpFilterGeneration();
            BumpFolderCheckGeneration();
        }

        public int BumpFilterGeneration() => Interlocked.Increment(ref _filterGeneration);

        public int FilterGeneration => Volatile.Read(ref _filterGeneration);

        public int BumpFolderCheckGeneration() => Interlocked.Increment(ref _folderCheckGeneration);

        public int FolderCheckGeneration => Volatile.Read(ref _folderCheckGeneration);

        public int BumpThumbnailWorkGeneration() => Interlocked.Increment(ref _thumbnailWorkGeneration);

        public int ThumbnailWorkGeneration => Volatile.Read(ref _thumbnailWorkGeneration);

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
