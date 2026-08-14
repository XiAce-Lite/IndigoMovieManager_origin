using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.Dmm;
namespace IndigoMovieManager
{
    public partial class MainWindow
    {
        private void RefreshDmmPendingMenuBadge()
        {
            if (MainVM?.DmmToolNavItems == null)
            {
                return;
            }

            NavigationDrawerItem item = MainVM.DmmToolNavItems
                .FirstOrDefault(nav => nav.Id == NavigationMenuIds.DmmPendingCandidates);
            if (item == null)
            {
                return;
            }

            string dbPath = MainVM.DbInfo?.DBFullPath;
            int count = string.IsNullOrEmpty(dbPath) ? 0 : DmmPendingCandidateStore.Count(dbPath);
            item.Text = count > 0
                ? $"{NavigationMenuIds.DmmPendingCandidates} ({count})"
                : NavigationMenuIds.DmmPendingCandidates;
        }

        private async void FetchDmmInfo_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _dmmFetchRunning, 1, 0) != 0)
            {
                MessageBox.Show(
                    this,
                    "手動の DMM 情報取得が実行中です。完了後に再度お試しください。",
                    "DMM情報取得",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                DmmApiOptions options = DmmApiOptions.FromSettings();
                if (!options.IsConfigured)
                {
                    var dialog = new MessageBoxEx(this)
                    {
                        DlogTitle = "DMM情報取得",
                        DlogMessage =
                            "DMM API ID / アフィリエイトID（API用）が未設定です。\n共通設定で入力してください。\n\nPowered by FANZA Webサービス",
                        PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CogOutline,
                        OkOnly = true,
                    };
                    dialog.ShowDialog();
                    return;
                }

                if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
                {
                    return;
                }

                List<MovieRecords> targets = GetSelectedMovies();
                if (targets == null || targets.Count == 0)
                {
                    return;
                }

                string dbPath = MainVM.DbInfo.DBFullPath;
                var client = new DmmItemListClient(options);
                var resolver = new DmmMetadataResolveService(client);
                var applier = new DmmMetadataApplyService();

                CountedProgressSession session = null;
                int applied = 0;
                int noCode = 0;
                int notFound = 0;
                int ambiguous = 0;
                int httpErrors = 0;

                try
                {
                    session = CountedProgressSession.BeginDmmFetch(targets.Count);
                    CancellationToken cancelToken = session.Cancel;
                    int done = 0;

                    foreach (MovieRecords rec in targets)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        string reportPath = rec.Movie_Path ?? rec.Movie_Name ?? "";
                        session.Report(done, reportPath);

                        DmmResolveResult resolved = await resolver
                            .ResolveAsync(rec.Movie_Name, cancelToken)
                            .ConfigureAwait(true);

                        switch (resolved.Outcome)
                        {
                            case DmmResolveOutcome.Applied:
                                applier.Apply(dbPath, rec, resolved.Item, action => RunOnUi(action));
                                applied++;
                                break;
                            case DmmResolveOutcome.NotConfigured:
                                httpErrors++;
                                break;
                            case DmmResolveOutcome.NoProductCode:
                            case DmmResolveOutcome.NotFound:
                            case DmmResolveOutcome.Ambiguous:
                            case DmmResolveOutcome.HttpError:
                                if (TryOpenDmmSearchDialog(rec, dbPath, resolved, out bool dialogApplied) && dialogApplied)
                                {
                                    applied++;
                                }
                                else if (resolved.Outcome == DmmResolveOutcome.NoProductCode)
                                {
                                    noCode++;
                                }
                                else if (resolved.Outcome == DmmResolveOutcome.NotFound)
                                {
                                    notFound++;
                                }
                                else if (resolved.Outcome == DmmResolveOutcome.Ambiguous)
                                {
                                    ambiguous++;
                                }
                                else
                                {
                                    httpErrors++;
                                }

                                break;
                        }

                        done++;
                        session.Report(done, reportPath);
                    }

                    session.Report(targets.Count, "完了");
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    session?.Dispose();
                }

                string summary =
                    $"成功 : {applied}\n" +
                    $"品番なし : {noCode}\n" +
                    $"未ヒット : {notFound}\n" +
                    $"複数候補 : {ambiguous}\n" +
                    $"エラー : {httpErrors}\n\n" +
                    "Powered by FANZA Webサービス";
                _statusBarProgress.ShowIdleStatusMessage(
                    $"DMM情報取得: 成功{applied} 品番なし{noCode} 未ヒット{notFound} 複数{ambiguous} エラー{httpErrors}");

                var doneDialog = new MessageBoxEx(this)
                {
                    DlogTitle = "DMM情報取得",
                    DlogMessage = summary,
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CloudDownloadOutline,
                    OkOnly = true,
                };
                doneDialog.ShowDialog();

                RunOnUi(() =>
                {
                    viewExtDetail.Refresh();
                    TabListRefreshHelper.RefreshActiveList(_currentSkinEngine, this);
                    RefreshDmmPendingMenuBadge();
                });
            }
            finally
            {
                Interlocked.Exchange(ref _dmmFetchRunning, 0);
                RunOnUi(RefreshDmmPendingMenuBadge);
            }
        }

        private void EnqueueAutoDmmFetchForDiscovered(IEnumerable<QueueObj> items)
        {
            if (!DmmApiOptions.FromSettings().IsConfigured || items == null)
            {
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            if (string.IsNullOrEmpty(dbPath))
            {
                return;
            }

            foreach (QueueObj item in items)
            {
                if (item == null || item.MovieId <= 0)
                {
                    continue;
                }

                string mediaPath = item.MovieFullPath;
                if (string.IsNullOrWhiteSpace(mediaPath))
                {
                    continue;
                }

                if (!WatchFolderDmmAutoService.IsEnabledForMediaPath(item.DbFullPath ?? dbPath, mediaPath))
                {
                    continue;
                }

                string movieName = Path.GetFileName(mediaPath);
                _dmmAutoFetchQueue.Enqueue(item.MovieId, movieName, item.DbFullPath ?? dbPath);
            }
        }

        private void BeginDmmBulkFetchFromMenu()
        {
            if (Volatile.Read(ref _dmmFetchRunning) == 1)
            {
                MessageBox.Show(
                    this,
                    "手動の DMM 情報取得が実行中です。完了後に再度お試しください。",
                    "DMM 情報を一括取得",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DmmApiOptions options = DmmApiOptions.FromSettings();
            if (!options.IsConfigured)
            {
                var dialog = new MessageBoxEx(this)
                {
                    DlogTitle = "DMM 情報を一括取得",
                    DlogMessage =
                        "DMM API ID / アフィリエイトID（API用）が未設定です。\n共通設定で入力してください。\n\nPowered by FANZA Webサービス",
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CogOutline,
                    OkOnly = true,
                };
                dialog.ShowDialog();
                return;
            }

            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            // 検索で絞り込んだ一覧を母数にする（未絞り込み時は全件表示と同じ）。
            IReadOnlyList<MovieRecords> scope = GetActiveFilterRecords();
            int scopeCount = scope.Count;
            string dbPath = MainVM.DbInfo.DBFullPath;
            HashSet<long> pendingMovieIds = DmmPendingCandidateStore.GetPendingMovieIds(dbPath);
            int pendingExcluded = 0;
            List<DmmAutoFetchJob> targets = [];
            foreach (MovieRecords record in scope)
            {
                if (!DmmMetadataEligibility.NeedsFetch(record.Title, record.Comment1))
                {
                    continue;
                }

                if (pendingMovieIds.Contains(record.Movie_Id))
                {
                    pendingExcluded++;
                    continue;
                }

                targets.Add(new DmmAutoFetchJob
                {
                    MovieId = record.Movie_Id,
                    MovieName = string.IsNullOrWhiteSpace(record.Movie_Path)
                        ? (record.Movie_Name ?? string.Empty)
                        : Path.GetFileName(record.Movie_Path),
                    DbPath = dbPath,
                    Source = "bulk",
                });
            }

            if (targets.Count == 0)
            {
                string emptyMessage = scopeCount == 0
                    ? "現在の一覧が空です。検索条件を確認してください。"
                    : pendingExcluded > 0
                        ? $"現在の一覧は {scopeCount} 件ですが、一括取得の対象はありません。\n（未確定候補 {pendingExcluded} 件は対象外です。タイトル／コメント1が既にある行も対象外です）"
                        : $"現在の一覧は {scopeCount} 件ですが、タイトルとコメント1が両方空のレコードはありません。\n（既に取得済み、または片方でも入力がある行は対象外です）";
                var emptyDialog = new MessageBoxEx(this)
                {
                    DlogTitle = "DMM 情報を一括取得",
                    DlogMessage = emptyMessage,
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline,
                    OkOnly = true,
                };
                emptyDialog.ShowDialog();
                return;
            }

            string pendingNote = pendingExcluded > 0
                ? $"\n未確定候補 {pendingExcluded} 件は対象外です。"
                : string.Empty;
            var confirmDialog = new MessageBoxEx(this)
            {
                DlogTitle = "DMM 情報を一括取得",
                DlogMessage =
                    $"現在の一覧 {scopeCount} 件のうち、メタデータ未設定の {targets.Count} 件に DMM 情報を取得します。{pendingNote}\n件数によっては長時間かかります。よろしいですか？\n\nPowered by FANZA Webサービス",
                PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CloudDownloadOutline,
            };
            confirmDialog.ShowDialog();
            if (confirmDialog.CloseStatus() != MessageBoxResult.OK)
            {
                return;
            }

            int added = _dmmAutoFetchQueue.EnqueueMany(targets);
            if (added == 0)
            {
                var busyDialog = new MessageBoxEx(this)
                {
                    DlogTitle = "DMM 情報を一括取得",
                    DlogMessage =
                        "対象はすでに取得キューに入っているか、投入できませんでした。\nステータスバーの進捗を確認するか、完了・キャンセル後に再度お試しください。",
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline,
                    OkOnly = true,
                };
                busyDialog.ShowDialog();
            }
        }

        private void FetchDmmManualSearch_Click(object sender, RoutedEventArgs e)
        {
            if (Volatile.Read(ref _dmmFetchRunning) == 1)
            {
                MessageBox.Show(
                    this,
                    "手動の DMM 情報取得が実行中です。完了後に再度お試しください。",
                    "DMM手動検索",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            DmmApiOptions options = DmmApiOptions.FromSettings();
            if (!options.IsConfigured)
            {
                var dialog = new MessageBoxEx(this)
                {
                    DlogTitle = "DMM手動検索",
                    DlogMessage =
                        "DMM API ID / アフィリエイトID（API用）が未設定です。\n共通設定で入力してください。\n\nPowered by FANZA Webサービス",
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CogOutline,
                    OkOnly = true,
                };
                dialog.ShowDialog();
                return;
            }

            if (string.IsNullOrEmpty(MainVM.DbInfo.DBFullPath))
            {
                return;
            }

            List<MovieRecords> targets = GetSelectedMovies();
            if (targets == null || targets.Count == 0)
            {
                return;
            }

            if (targets.Count != 1)
            {
                MessageBox.Show(
                    this,
                    "DMM手動検索は1件選択時のみ利用できます。",
                    "DMM手動検索",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MovieRecords rec = targets[0];
            string dbPath = MainVM.DbInfo.DBFullPath;
            string initialKeyword = DmmInitialKeyword.FromMovieName(rec.Movie_Name);
            if (TryOpenDmmSearchDialog(rec, dbPath, initialKeyword, null, out bool applied) && applied)
            {
                RunOnUi(() =>
                {
                    viewExtDetail.Refresh();
                    TabListRefreshHelper.RefreshActiveList(_currentSkinEngine, this);
                });
            }
        }

        private bool TryOpenDmmSearchDialog(
            MovieRecords rec,
            string dbPath,
            DmmResolveResult resolved,
            out bool applied)
        {
            string initialKeyword = resolved?.InitialKeyword;
            if (string.IsNullOrWhiteSpace(initialKeyword))
            {
                initialKeyword = DmmInitialKeyword.FromMovieName(rec.Movie_Name);
            }

            IReadOnlyList<DmmCandidateEntry> initialCandidates = resolved?.Candidates;
            return TryOpenDmmSearchDialog(rec, dbPath, initialKeyword, initialCandidates, out applied);
        }

        private bool TryOpenDmmSearchDialog(
            MovieRecords rec,
            string dbPath,
            string initialKeyword,
            IReadOnlyList<DmmCandidateEntry> initialCandidates,
            out bool applied)
        {
            applied = false;
            var searchWindow = new DmmSearchWindow(rec, dbPath, initialKeyword, initialCandidates)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            searchWindow.ShowDialog();
            applied = searchWindow.AppliedSuccessfully;
            return true;
        }

        private void BeginDmmPendingCandidatesFromMenu()
        {
            DmmApiOptions options = DmmApiOptions.FromSettings();
            if (!options.IsConfigured)
            {
                var dialog = new MessageBoxEx(this)
                {
                    DlogTitle = NavigationMenuIds.DmmPendingCandidates,
                    DlogMessage =
                        "DMM API ID / アフィリエイトID（API用）が未設定です。\n共通設定で入力してください。\n\nPowered by FANZA Webサービス",
                    PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CogOutline,
                    OkOnly = true,
                };
                dialog.ShowDialog();
                return;
            }

            string dbPath = MainVM.DbInfo.DBFullPath;
            DmmPendingCandidateStore.EnsureTable(dbPath);

            var pendingWindow = new DmmPendingCandidatesWindow(
                dbPath,
                movieId => MainVM.MovieRecs?.FirstOrDefault(record => record.Movie_Id == movieId),
                () =>
                {
                    RunOnUi(() =>
                    {
                        viewExtDetail.Refresh();
                        TabListRefreshHelper.RefreshActiveList(_currentSkinEngine, this);
                        RefreshDmmPendingMenuBadge();
                    });
                })
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            pendingWindow.ShowDialog();
            RefreshDmmPendingMenuBadge();
        }

        private sealed class MainWindowDmmAutoFetchHost : IDmmAutoFetchHost
        {
            private readonly MainWindow _owner;

            public MainWindowDmmAutoFetchHost(MainWindow owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public bool IsManualFetchRunning => Volatile.Read(ref _owner._dmmFetchRunning) == 1;

            public void RunOnUi(Action action) =>
                UiDispatcherHelper.RunOnUi(_owner.Dispatcher, action);

            public Task RunOnUiAsync(Action action) =>
                UiDispatcherHelper.RunOnUiAsync(_owner.Dispatcher, action);

            public MovieRecords FindMovieRecord(long movieId) =>
                _owner.MainVM?.MovieRecs?.FirstOrDefault(record => record.Movie_Id == movieId);

            public void NotifyRecordUpdated(long movieId)
            {
                _ = UiDispatcherHelper.RunOnUiAsync(_owner.Dispatcher, () =>
                {
                    if (_owner.viewExtDetail.DataContext is MovieRecords detail && detail.Movie_Id == movieId)
                    {
                        _owner.viewExtDetail.Refresh();
                    }

                    TabListRefreshHelper.RefreshActiveList(_owner._currentSkinEngine, _owner);
                });
            }

            public void NotifyPendingCandidatesChanged() =>
                _ = UiDispatcherHelper.RunOnUiAsync(_owner.Dispatcher, _owner.RefreshDmmPendingMenuBadge);

            public void ShowCompletionMessage(string message)
            {
                _owner._statusBarProgress.ShowIdleStatusMessage(message);
                _ = UiDispatcherHelper.RunOnUiAsync(_owner.Dispatcher, _owner.RefreshDmmPendingMenuBadge);
            }

            public void ShowCompletionDialog(string title, string message)
            {
                _ = UiDispatcherHelper.RunOnUiAsync(_owner.Dispatcher, () =>
                {
                    var dialog = new MessageBoxEx(_owner)
                    {
                        DlogTitle = title,
                        DlogMessage = message,
                        PackIconKind = MaterialDesignThemes.Wpf.PackIconKind.CloudDownloadOutline,
                        OkOnly = true,
                    };
                    dialog.ShowDialog();
                    _owner.RefreshDmmPendingMenuBadge();
                });
            }
        }
    }
}
