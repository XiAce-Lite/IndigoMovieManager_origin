namespace IndigoMovieManager.Services
{
    /// <summary>
    /// 監視イベント等で同一ファイルへの登録処理が並行実行されないよう排他する。
    /// </summary>
    internal sealed class DiscoveredFileRegistrationGate
    {
        private readonly HashSet<string> _inFlight = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public bool TryEnter(string normalizedPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            lock (_lock)
            {
                return _inFlight.Add(normalizedPath);
            }
        }

        public void Exit(string normalizedPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            lock (_lock)
            {
                _inFlight.Remove(normalizedPath);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _inFlight.Clear();
            }
        }
    }
}
