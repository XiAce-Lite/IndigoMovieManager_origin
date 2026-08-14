using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
namespace IndigoMovieManager
{
    public partial class MainWindow
    {
        private void EnqueueDiscoveredFileThumbnails(MovieInfo mvi, string dbFullPath)
        {
            CancelThumbnailWorkForMovie(mvi.MovieId);
            var queueItem = new QueueObj
            {
                MovieId = mvi.MovieId,
                MovieFullPath = mvi.MoviePath,
                DbFullPath = dbFullPath,
            };

            lock (_pendingDiscoveredThumbnailLock)
            {
                _pendingDiscoveredThumbnailWork.Add(queueItem);
            }
        }

        private void TryScheduleDiscoveredThumbnailFlush()
        {
            lock (_pendingDiscoveredThumbnailLock)
            {
                if (_pendingDiscoveredThumbnailWork.Count == 0)
                {
                    return;
                }

                ScheduleDiscoveredThumbnailFlushLocked();
            }
        }

        private void ScheduleDiscoveredThumbnailFlushLocked()
        {
            _discoveredThumbnailFlushCts?.Cancel();
            _discoveredThumbnailFlushCts?.Dispose();
            _discoveredThumbnailFlushCts = new CancellationTokenSource();
            CancellationTokenSource flushCts = _discoveredThumbnailFlushCts;
            _ = FlushDiscoveredThumbnailBatchAsync(flushCts);
        }

        private void ClearPendingDiscoveredThumbnailWork()
        {
            lock (_pendingDiscoveredThumbnailLock)
            {
                _pendingDiscoveredThumbnailWork.Clear();
                _discoveredThumbnailFlushCts?.Cancel();
                _discoveredThumbnailFlushCts?.Dispose();
                _discoveredThumbnailFlushCts = null;
            }

            Interlocked.Exchange(ref _discoveredRegistrationInFlight, 0);
        }

        private async Task FlushDiscoveredThumbnailBatchAsync(CancellationTokenSource flushCts)
        {
            try
            {
                await Task.Delay(DiscoveredThumbnailFlushDelayMs, flushCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (Volatile.Read(ref _discoveredRegistrationInFlight) > 0)
            {
                TryScheduleDiscoveredThumbnailFlush();
                return;
            }

            List<QueueObj> batch;
            lock (_pendingDiscoveredThumbnailLock)
            {
                if (flushCts.IsCancellationRequested || _pendingDiscoveredThumbnailWork.Count == 0)
                {
                    return;
                }

                batch = [.. _pendingDiscoveredThumbnailWork];
                _pendingDiscoveredThumbnailWork.Clear();
            }

            await Dispatcher.InvokeAsync(async () =>
            {
                if (batch.Count == 0)
                {
                    return;
                }

                foreach (QueueObj item in batch)
                {
                    PopulateActiveListQueueLayout(item);
                }

                string sortId = MainVM.DbInfo.Sort ?? "1";
                await FilterAndSortAsync(sortId, true).ConfigureAwait(true);
                EnqueueThumbnailWork(batch, beginNewJob: ShouldBeginNewDiscoveredThumbnailJob());
                EnqueueAutoDmmFetchForDiscovered(batch);
            }).Task.Unwrap().ConfigureAwait(false);
        }

        /// <summary>
        /// 監視で連続検知された複数ファイルを同一ジョブにまとめる。
        /// 毎回 beginNewJob すると先行分が破棄され 0/1 表示のまま1件しか処理されない。
        /// タブ切替の全件スキャンと競合しても、進行中ジョブは捨てない。
        /// </summary>
        private bool ShouldBeginNewDiscoveredThumbnailJob() =>
            _thumbnailScheduler.ShouldBeginNewVisibleJob(GetActiveListLayoutKey());

        /// <summary>
        /// ファイル追加
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileChanged(FileSystemEventArgs e, int watcherSession) =>
            _ = HandleFileChangedAsync(e, watcherSession);

        private async Task HandleFileChangedAsync(FileSystemEventArgs e, int watcherSession)
        {
            if (!_fileWatcherManager.IsSessionActive(watcherSession))
            {
                return;
            }

            try
            {
                (bool shouldProcess, string dbPath) = await Dispatcher.InvokeAsync(() =>
                {
                    if (!_fileWatcherManager.IsSessionActive(watcherSession))
                    {
                        return (false, "");
                    }

                    if (e.ChangeType != WatcherChangeTypes.Created
                        && e.ChangeType != WatcherChangeTypes.Changed)
                    {
                        return (false, "");
                    }

                    if (!MediaExtensionSettings.ShouldScanFile(
                            e.FullPath,
                            Properties.Settings.Default.CheckExt,
                            MainVM.DbInfo.ExcludeExt))
                    {
                        return (false, "");
                    }

                    string path = MainVM.DbInfo.DBFullPath;
                    return (!string.IsNullOrWhiteSpace(path), path);
                }).Task.ConfigureAwait(false);

                if (!shouldProcess || string.IsNullOrWhiteSpace(dbPath))
                {
                    return;
                }

                const int maxRetry = 10;
                int retry = 0;
                bool fileReady = false;
                while (retry < maxRetry)
                {
                    try
                    {
                        using var stream = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        fileReady = true;
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        retry++;
                    }
                }

                if (!fileReady)
                {
#if DEBUG
                    Debug.WriteLine($"ファイル {e.FullPath} にアクセスできません。");
#endif
                    return;
                }

                if (!_fileWatcherManager.IsSessionActive(watcherSession))
                {
                    return;
                }

                string normalizedPath = MediaPathNormalizer.Normalize(e.FullPath);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    return;
                }

                if (!_discoveredFileRegistrationGate.TryEnter(normalizedPath))
                {
#if DEBUG
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [watcher] skip duplicate in-flight: {normalizedPath}");
#endif
                    return;
                }

                Interlocked.Increment(ref _discoveredRegistrationInFlight);
                try
                {
                    bool alreadyRegistered = await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_fileWatcherManager.IsSessionActive(watcherSession))
                        {
                            return true;
                        }

                        return !FolderCheckService.ShouldRegisterDiscoveredFile(dbPath, e.FullPath);
                    }).Task.ConfigureAwait(false);

                    if (alreadyRegistered)
                    {
                        return;
                    }

                    MovieInfo mvi = await MovieRegistrationHelper
                        .TryRegisterDiscoveredFileAsync(dbPath, e.FullPath)
                        .ConfigureAwait(false);
                    if (mvi == null)
                    {
                        return;
                    }

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!_fileWatcherManager.IsSessionActive(watcherSession))
                        {
                            return;
                        }

                        EnqueueDiscoveredFileThumbnails(mvi, dbPath);
                    }).Task.ConfigureAwait(false);
                }
                finally
                {
                    _discoveredFileRegistrationGate.Exit(normalizedPath);
                    if (Interlocked.Decrement(ref _discoveredRegistrationInFlight) == 0)
                    {
                        TryScheduleDiscoveredThumbnailFlush();
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"FileChangedで例外発生: {ex.Message}");
#endif
                await UiDispatcherHelper.RunOnUiAsync(
                    Dispatcher,
                    () => MessageBox.Show(
                        this,
                        $"ファイル変更の処理中にエラーが発生しました。\n{ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
            }
        }

        /// <summary>
        /// ファイル名変更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileRenamed(RenamedEventArgs e, int watcherSession)
        {
            if (!_fileWatcherManager.IsSessionActive(watcherSession))
            {
                return;
            }

            var eFullPath = e.FullPath;
            var oldFullPath = e.OldFullPath;

            _ = Dispatcher.InvokeAsync(() =>
            {
                if (!_fileWatcherManager.IsSessionActive(watcherSession))
                {
                    return;
                }

                if (!MediaExtensionSettings.ShouldScanFile(
                        eFullPath,
                        Properties.Settings.Default.CheckExt,
                        MainVM.DbInfo.ExcludeExt))
                {
                    return;
                }

#if DEBUG
                string s = string.Format($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} :");
                s += $"【{e.ChangeType}】{e.OldName} → {eFullPath}";
                Debug.WriteLine(s);
#endif
                _ = RenameThumb(eFullPath, oldFullPath);
            });
        }

        private void RunWatcher(string watchFolder, bool sub) =>
            _fileWatcherManager.AddWatcher(watchFolder, sub, FileChanged, FileRenamed);
    }
}
