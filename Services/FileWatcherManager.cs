using System.IO;

namespace IndigoMovieManager.Services
{
    internal sealed class FileWatcherManager
    {
        private readonly List<FileSystemWatcher> _watchers = [];
        private int _sessionId;

        public IReadOnlyList<FileSystemWatcher> Watchers => _watchers;

        public int CurrentSessionId => Volatile.Read(ref _sessionId);

        public bool IsSessionActive(int sessionId) => sessionId == CurrentSessionId;

        public void Clear()
        {
            foreach (FileSystemWatcher watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch
                {
                }
            }

            _watchers.Clear();
            Interlocked.Increment(ref _sessionId);
        }

        public void AddWatcher(
            string watchFolder,
            bool sub,
            Action<FileSystemEventArgs, int> onChanged,
            Action<RenamedEventArgs, int> onRenamed)
        {
            if (!Path.Exists(watchFolder))
            {
                return;
            }

            int session = CurrentSessionId;
            FileSystemWatcher item = new()
            {
                Path = watchFolder,
                Filter = "",
                NotifyFilter = NotifyFilters.LastAccess |
                               NotifyFilters.LastWrite |
                               NotifyFilters.FileName |
                               NotifyFilters.DirectoryName,
                IncludeSubdirectories = sub,
                InternalBufferSize = 1024 * 32
            };

            item.Changed += (_, e) => onChanged(e, session);
            item.Created += (_, e) => onChanged(e, session);
            item.Renamed += (_, e) => onRenamed(e, session);
            item.EnableRaisingEvents = true;
            _watchers.Add(item);
        }
    }
}
