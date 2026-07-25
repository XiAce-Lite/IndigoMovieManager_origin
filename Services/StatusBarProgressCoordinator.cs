using System.Windows.Threading;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// サムネイル・フォルダ監視・ファイル情報再取得の進捗を、優先度付きでステータスバーに表示する。
    /// </summary>
    internal sealed class StatusBarProgressCoordinator
    {
        private readonly Dispatcher _dispatcher;
        private readonly object _sync = new();
        private readonly StatusBarProgressViewModel _viewModel = new();

        private ThumbnailSlot _thumbnail;
        private FolderCheckSlot _folderCheck;
        private FileInfoSlot _fileInfo;
        private JacketFetchSlot _jacketFetch;

        public StatusBarProgressCoordinator(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public StatusBarProgressViewModel ViewModel => _viewModel;

        public ThumbnailSlotHandle BeginThumbnail(string baseTitle)
        {
            lock (_sync)
            {
                _thumbnail?.Deactivate();
                var slot = new ThumbnailSlot(baseTitle);
                _thumbnail = slot;
                RefreshDisplayLocked();
                return new ThumbnailSlotHandle(this, slot);
            }
        }

        public FileInfoSlotHandle BeginFileInfo(int total, string statusLabel = null)
        {
            lock (_sync)
            {
                _fileInfo?.Deactivate();
                var slot = new FileInfoSlot(total, statusLabel);
                _fileInfo = slot;
                RefreshDisplayLocked();
                return new FileInfoSlotHandle(this, slot);
            }
        }

        public FolderCheckSlotHandle BeginFolderCheck(int total)
        {
            lock (_sync)
            {
                _folderCheck?.Deactivate();
                var slot = new FolderCheckSlot(total);
                _folderCheck = slot;
                RefreshDisplayLocked();
                return new FolderCheckSlotHandle(this, slot);
            }
        }

        public void RequestCancelActive()
        {
            FileInfoSlot fileInfo;
            lock (_sync)
            {
                fileInfo = _fileInfo;
            }

            fileInfo?.RequestCancel();
        }

        public void ShowIdleStatusMessage(string message, int durationMs = 2500)
        {
            int generation = Interlocked.Increment(ref _transientStatusGeneration);
            lock (_sync)
            {
                if (GetVisibleSlotLocked() != null)
                {
                    return;
                }
            }

            RunOnUi(() => _viewModel.StatusText = string.IsNullOrWhiteSpace(message) ? "準備完了" : message);

            _ = Task.Delay(durationMs).ContinueWith(_ =>
            {
                if (generation != _transientStatusGeneration)
                {
                    return;
                }

                lock (_sync)
                {
                    if (GetVisibleSlotLocked() != null)
                    {
                        return;
                    }
                }

                RunOnUi(() => _viewModel.StatusText = "準備完了");
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// ジャケ写 URL 取得の進行中件数。他スロットが無いときステータスバーに表示する。
        /// </summary>
        public void SetJacketFetchInFlight(int count)
        {
            lock (_sync)
            {
                if (count <= 0)
                {
                    if (_jacketFetch != null)
                    {
                        _jacketFetch.Deactivate();
                        _jacketFetch = null;
                        RefreshDisplayLocked();
                    }

                    return;
                }

                if (_jacketFetch == null)
                {
                    _jacketFetch = new JacketFetchSlot();
                }

                _jacketFetch.Update(count);
                RefreshDisplayLocked();
            }
        }

        private int _transientStatusGeneration;

        private void ReleaseThumbnail(ThumbnailSlot slot)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_thumbnail, slot))
                {
                    return;
                }

                _thumbnail = null;
                RefreshDisplayLocked();
            }
        }

        private void ReleaseFileInfo(FileInfoSlot slot)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_fileInfo, slot))
                {
                    return;
                }

                _fileInfo = null;
                RefreshDisplayLocked();
            }
        }

        private void ReleaseFolderCheck(FolderCheckSlot slot)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_folderCheck, slot))
                {
                    return;
                }

                _folderCheck = null;
                RefreshDisplayLocked();
            }
        }

        private void ReportThumbnail(ThumbnailSlot slot, string title, int percent, string detail)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_thumbnail, slot) || !slot.IsActive)
                {
                    return;
                }

                slot.Update(title, percent, detail);
                RefreshDisplayLocked();
            }
        }

        private void ReportFileInfo(FileInfoSlot slot, int done, string detail)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_fileInfo, slot) || !slot.IsActive)
                {
                    return;
                }

                slot.Update(done, detail);
                RefreshDisplayLocked();
            }
        }

        private void ReportFolderCheck(FolderCheckSlot slot, int done, string detail)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_folderCheck, slot) || !slot.IsActive)
                {
                    return;
                }

                slot.Update(done, detail);
                RefreshDisplayLocked();
            }
        }

        private void RefreshDisplayLocked()
        {
            ISlotState active = GetVisibleSlotLocked();
            if (active == null)
            {
                RunOnUi(() =>
                {
                    _viewModel.IsActive = false;
                    _viewModel.StatusText = "準備完了";
                    _viewModel.ProgressPercent = 0;
                    _viewModel.ShowProgress = false;
                    _viewModel.ShowCancel = false;
                });
                return;
            }

            string statusText = active.BuildStatusText();
            double percent = active.ProgressPercent;
            bool showProgress = active.ShowProgress;
            bool showCancel = active.Kind == StatusBarProgressKind.FileInfoRefresh;

            RunOnUi(() =>
            {
                _viewModel.IsActive = true;
                _viewModel.StatusText = statusText;
                _viewModel.ProgressPercent = percent;
                _viewModel.ShowProgress = showProgress;
                _viewModel.ShowCancel = showCancel;
            });
        }

        private ISlotState GetVisibleSlotLocked()
        {
            if (_fileInfo?.IsActive == true)
            {
                return _fileInfo;
            }

            if (_folderCheck?.IsActive == true)
            {
                return _folderCheck;
            }

            if (_thumbnail?.IsActive == true)
            {
                return _thumbnail;
            }

            if (_jacketFetch?.IsActive == true)
            {
                return _jacketFetch;
            }

            return null;
        }

        private void RunOnUi(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _dispatcher.BeginInvoke(action, DispatcherPriority.DataBind);
        }

        private interface ISlotState
        {
            StatusBarProgressKind Kind { get; }
            bool IsActive { get; }
            bool ShowProgress { get; }
            double ProgressPercent { get; }
            string BuildStatusText();
        }

        internal sealed class ThumbnailSlot : ISlotState
        {
            private readonly string _baseTitle;
            private string _title;
            private string _detail = "";
            private int _percent;

            public ThumbnailSlot(string baseTitle)
            {
                _baseTitle = baseTitle ?? "";
                _title = _baseTitle;
            }

            public StatusBarProgressKind Kind => StatusBarProgressKind.Thumbnail;

            public bool IsActive { get; private set; } = true;

            public bool ShowProgress => true;

            public double ProgressPercent => _percent;

            public void Deactivate() => IsActive = false;

            public void Update(string title, int percent, string detail)
            {
                _title = string.IsNullOrEmpty(title) ? _baseTitle : title;
                _percent = Math.Clamp(percent, 0, 100);
                _detail = detail ?? "";
            }

            public string BuildStatusText()
            {
                if (string.IsNullOrEmpty(_detail))
                {
                    return _title;
                }

                return $"{_title}  {_detail}";
            }
        }

        internal sealed class FileInfoSlot : ISlotState
        {
            private readonly CancellationTokenSource _cts = new();
            private readonly int _total;
            private readonly string _statusLabel;
            private int _done;
            private string _detail = "";
            private bool _ctsDisposed;

            public FileInfoSlot(int total, string statusLabel = null)
            {
                _total = Math.Max(0, total);
                _statusLabel = string.IsNullOrWhiteSpace(statusLabel)
                    ? "ファイル情報再取得中"
                    : statusLabel.Trim();
            }

            public StatusBarProgressKind Kind => StatusBarProgressKind.FileInfoRefresh;

            public bool IsActive { get; private set; } = true;

            public bool ShowProgress => _total > 0;

            public CancellationToken Cancel => _cts.Token;

            public double ProgressPercent
            {
                get
                {
                    if (_total <= 0)
                    {
                        return 100;
                    }

                    int done = Math.Clamp(_done, 0, _total);
                    return done * 100d / _total;
                }
            }

            public void Deactivate()
            {
                IsActive = false;
                if (_ctsDisposed)
                {
                    return;
                }

                try
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                }
                catch
                {
                }

                _ctsDisposed = true;
                _cts.Dispose();
            }

            public void RequestCancel()
            {
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }

            public void Update(int done, string detail)
            {
                _done = done;
                _detail = detail ?? "";
            }

            public string BuildStatusText()
            {
                string count = _total > 0 ? $" ({_done}/{_total})" : "";
                string detail = string.IsNullOrWhiteSpace(_detail) ? "" : $"  {_detail}";
                return $"{_statusLabel}{count}{detail}";
            }
        }

        internal sealed class FolderCheckSlot : ISlotState
        {
            private readonly int _total;
            private int _done;
            private string _detail = "";

            public FolderCheckSlot(int total)
            {
                _total = Math.Max(0, total);
            }

            public int Total => _total;

            public StatusBarProgressKind Kind => StatusBarProgressKind.FolderCheck;

            public bool IsActive { get; private set; } = true;

            public bool ShowProgress => _total > 0;

            public double ProgressPercent
            {
                get
                {
                    if (_total <= 0)
                    {
                        return 100;
                    }

                    int done = Math.Clamp(_done, 0, _total);
                    return done * 100d / _total;
                }
            }

            public void Deactivate() => IsActive = false;

            public void Update(int done, string detail)
            {
                _done = done;
                _detail = detail ?? "";
            }

            public string BuildStatusText()
            {
                string message = _total > 1
                    ? $"({_done}/{_total}) {_detail}"
                    : _detail;
                return string.IsNullOrWhiteSpace(message)
                    ? "フォルダ監視中"
                    : $"フォルダ監視中  {message}";
            }
        }

        internal sealed class JacketFetchSlot : ISlotState
        {
            private int _inFlight;

            public StatusBarProgressKind Kind => StatusBarProgressKind.JacketFetch;

            public bool IsActive { get; private set; } = true;

            // 件数表示が主。確定%が無いのでバーは出さない
            public bool ShowProgress => false;

            // 件数ベースの確定進捗ではないのでインジケータは満タン寄りに見せない
            public double ProgressPercent => 0;

            public void Deactivate() => IsActive = false;

            public void Update(int inFlight)
            {
                _inFlight = Math.Max(0, inFlight);
                IsActive = _inFlight > 0;
            }

            public string BuildStatusText() =>
                _inFlight <= 0 ? "準備完了" : $"ジャケ写取得中  {_inFlight} 件";
        }

        internal sealed class ThumbnailSlotHandle : IDisposable
        {
            private readonly StatusBarProgressCoordinator _owner;
            private readonly ThumbnailSlot _slot;
            private bool _disposed;

            internal ThumbnailSlotHandle(StatusBarProgressCoordinator owner, ThumbnailSlot slot)
            {
                _owner = owner;
                _slot = slot;
            }

            public void Report(string title, int percent, string detail)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.ReportThumbnail(_slot, title, percent, detail);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _slot.Deactivate();
                _owner.ReleaseThumbnail(_slot);
            }
        }

        internal sealed class FileInfoSlotHandle : IDisposable
        {
            private readonly StatusBarProgressCoordinator _owner;
            private readonly FileInfoSlot _slot;
            private bool _disposed;

            internal FileInfoSlotHandle(StatusBarProgressCoordinator owner, FileInfoSlot slot)
            {
                _owner = owner;
                _slot = slot;
            }

            public CancellationToken Cancel => _slot.Cancel;

            public void Report(int done, string detail)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.ReportFileInfo(_slot, done, detail);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _slot.Deactivate();
                _owner.ReleaseFileInfo(_slot);
            }
        }

        internal sealed class FolderCheckSlotHandle : IDisposable
        {
            private readonly StatusBarProgressCoordinator _owner;
            private readonly FolderCheckSlot _slot;
            private bool _disposed;

            internal FolderCheckSlotHandle(StatusBarProgressCoordinator owner, FolderCheckSlot slot)
            {
                _owner = owner;
                _slot = slot;
            }

            public void Report(int done, string detail)
            {
                if (_disposed)
                {
                    return;
                }

                _owner.ReportFolderCheck(_slot, done, detail);
            }

            public void Complete()
            {
                if (_disposed)
                {
                    return;
                }

                Report(_slot.Total, "監視完了");
                Dispose();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _slot.Deactivate();
                _owner.ReleaseFolderCheck(_slot);
            }
        }
    }
}
