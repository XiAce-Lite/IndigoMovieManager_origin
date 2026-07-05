namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// DB hash と実ファイル hash の同期に使うコールバック（テストではモック可）。
    /// </summary>
    internal sealed class ThumbnailHashSyncContext
    {
        public string DbFullPath { get; init; }
        public Func<string, string> ComputeFileHash { get; init; }
        public Action<long, string> UpdateDbHash { get; init; }
    }
}
