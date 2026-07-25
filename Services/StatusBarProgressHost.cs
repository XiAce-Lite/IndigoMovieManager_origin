namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 進捗セッションからステータスバー表示へアクセスするためのホスト。
    /// </summary>
    internal static class StatusBarProgressHost
    {
        private static StatusBarProgressCoordinator _coordinator;

        public static void Attach(StatusBarProgressCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public static StatusBarProgressCoordinator Coordinator =>
            _coordinator ?? throw new InvalidOperationException("StatusBarProgressHost is not initialized.");

        public static StatusBarProgressCoordinator CoordinatorOrNull => _coordinator;
    }
}
