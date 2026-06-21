using System.IO;

namespace IndigoMovieManager.Services
{
    internal sealed class FileWatcherManager
    {
        private readonly List<FileSystemWatcher> _watchers = [];

        public IReadOnlyList<FileSystemWatcher> Watchers => _watchers;

        public void Clear()
        {
            _watchers.Clear();
        }

        public void AddWatcher(string watchFolder, bool sub, FileSystemEventHandler onChanged, RenamedEventHandler onRenamed)
        {
            if (!Path.Exists(watchFolder))
            {
                return;
            }

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

            item.Changed += onChanged;
            item.Created += onChanged;
            item.Renamed += onRenamed;
            item.EnableRaisingEvents = true;
            _watchers.Add(item);
        }
    }
}
