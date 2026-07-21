using IndigoMovieManager;

namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmAutoFetchJob
    {
        public long MovieId { get; init; }
        public string MovieName { get; init; }
        public string DbPath { get; init; }
        public string Source { get; init; } = "auto";
    }

    /// <summary>
    /// 新規登録・一括取得向けの DMM 自動取得キュー（直列実行）。
    /// </summary>
    internal sealed class DmmAutoFetchQueue : IDisposable
    {
        private readonly IDmmAutoFetchHost _host;
        private readonly object _sync = new();
        private readonly Queue<DmmAutoFetchJob> _queue = new();
        private readonly HashSet<long> _pendingIds = new();
        private readonly CancellationTokenSource _appCts = new();
        private readonly Task _workerTask;

        private DmmFetchProgressSession _session;
        private int _batchExpectedTotal;
        private int _batchDone;
        private int _batchApplied;
        private int _batchNoCode;
        private int _batchNotFound;
        private int _batchAmbiguous;
        private int _batchErrors;
        private int _batchSkipped;
        private bool _batchIncludesBulk;
        private bool _batchCancelled;

        public DmmAutoFetchQueue(IDmmAutoFetchHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _workerTask = Task.Run(() => WorkerLoopAsync(_appCts.Token));
        }

        public bool Enqueue(long movieId, string movieName, string dbPath, string source = "auto")
        {
            if (movieId <= 0 || string.IsNullOrWhiteSpace(dbPath))
            {
                return false;
            }

            return EnqueueJob(new DmmAutoFetchJob
            {
                MovieId = movieId,
                MovieName = movieName ?? string.Empty,
                DbPath = dbPath,
                Source = string.IsNullOrWhiteSpace(source) ? "auto" : source,
            });
        }

        public int EnqueueMany(IEnumerable<DmmAutoFetchJob> jobs)
        {
            if (jobs == null)
            {
                return 0;
            }

            // 一括投入中にワーカーが先頭だけ見て total=1 にならないよう、まとめて lock する。
            lock (_sync)
            {
                int added = 0;
                foreach (DmmAutoFetchJob job in jobs)
                {
                    if (job != null && EnqueueJobLocked(job))
                    {
                        added++;
                    }
                }

                return added;
            }
        }

        public void Dispose()
        {
            _appCts.Cancel();
            try
            {
                _workerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
            }

            _appCts.Dispose();
            EndSessionAsync().GetAwaiter().GetResult();
        }

        private bool EnqueueJob(DmmAutoFetchJob job)
        {
            if (job == null || job.MovieId <= 0 || string.IsNullOrWhiteSpace(job.DbPath))
            {
                return false;
            }

            lock (_sync)
            {
                return EnqueueJobLocked(job);
            }
        }

        private bool EnqueueJobLocked(DmmAutoFetchJob job)
        {
            if (job == null || job.MovieId <= 0 || string.IsNullOrWhiteSpace(job.DbPath))
            {
                return false;
            }

            if (_pendingIds.Contains(job.MovieId))
            {
                return false;
            }

            _pendingIds.Add(job.MovieId);
            _queue.Enqueue(new DmmAutoFetchJob
            {
                MovieId = job.MovieId,
                MovieName = job.MovieName ?? string.Empty,
                DbPath = job.DbPath,
                Source = string.IsNullOrWhiteSpace(job.Source) ? "auto" : job.Source,
            });
            return true;
        }

        private async Task WorkerLoopAsync(CancellationToken appToken)
        {
            while (!appToken.IsCancellationRequested)
            {
                try
                {
                    while (_host.IsManualFetchRunning && !appToken.IsCancellationRequested)
                    {
                        await Task.Delay(200, appToken).ConfigureAwait(false);
                    }

                    DmmAutoFetchJob job = DequeueJob();
                    if (job == null)
                    {
                        await FinishBatchIfIdleAsync().ConfigureAwait(false);
                        try
                        {
                            await Task.Delay(200, appToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        continue;
                    }

                    await EnsureSessionStartedAsync(job).ConfigureAwait(false);
                    await ProcessJobAsync(job, appToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (appToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // ワーカーを落とさず、次のジョブへ進む。
                    Interlocked.Increment(ref _batchErrors);
                }
            }

            await FinishBatchIfIdleAsync().ConfigureAwait(false);
        }

        private DmmAutoFetchJob DequeueJob()
        {
            lock (_sync)
            {
                if (_queue.Count == 0)
                {
                    return null;
                }

                DmmAutoFetchJob job = _queue.Dequeue();
                _pendingIds.Remove(job.MovieId);
                return job;
            }
        }

        private async Task EnsureSessionStartedAsync(DmmAutoFetchJob firstJob)
        {
            if (_session != null)
            {
                if (IsBulkSource(firstJob.Source))
                {
                    _batchIncludesBulk = true;
                }

                return;
            }

            int total;
            lock (_sync)
            {
                total = _queue.Count + 1;
            }

            _batchExpectedTotal = Math.Max(total, 1);
            _batchDone = 0;
            _batchApplied = 0;
            _batchNoCode = 0;
            _batchNotFound = 0;
            _batchAmbiguous = 0;
            _batchErrors = 0;
            _batchSkipped = 0;
            _batchCancelled = false;
            _batchIncludesBulk = IsBulkSource(firstJob?.Source);

            // ProgressPathFormatter / BeginFileInfo は UI スレッド前提。
            int sessionTotal = _batchExpectedTotal;
            await _host.RunOnUiAsync(() =>
            {
                _session = new DmmFetchProgressSession(sessionTotal);
            }).ConfigureAwait(false);
        }

        private async Task ProcessJobAsync(DmmAutoFetchJob job, CancellationToken appToken)
        {
            CancellationToken cancelToken = _session?.Cancel ?? appToken;
            string reportPath = job.MovieName;

            try
            {
                cancelToken.ThrowIfCancellationRequested();

                if (!DmmApiOptions.FromSettings().IsConfigured)
                {
                    Interlocked.Increment(ref _batchSkipped);
                    return;
                }

                MovieRecords rec = _host.FindMovieRecord(job.MovieId);
                if (rec != null && !DmmMetadataEligibility.NeedsFetch(rec.Title, rec.Comment1))
                {
                    Interlocked.Increment(ref _batchSkipped);
                    return;
                }

                string resolveName = ResolveMovieName(job, rec);
                reportPath = resolveName;

                // 手動取得と同様、Resolve 前に現在処理中のファイル名を出す。
                await ReportProgressAsync(_batchDone, reportPath).ConfigureAwait(false);

                var client = new DmmItemListClient(DmmApiOptions.FromSettings());
                var resolver = new DmmMetadataResolveService(client);
                var applier = new DmmMetadataApplyService();

                DmmResolveResult resolved = await resolver
                    .ResolveAsync(resolveName, cancelToken)
                    .ConfigureAwait(false);

                switch (resolved.Outcome)
                {
                    case DmmResolveOutcome.Applied:
                    {
                        MovieRecords target = _host.FindMovieRecord(job.MovieId);
                        if (target == null)
                        {
                            Interlocked.Increment(ref _batchSkipped);
                            break;
                        }

                        // HTTP（裏面ジャケット）と DB 書き込みはワーカー。UI へはプロパティ代入だけ。
                        applier.Apply(
                            job.DbPath,
                            target,
                            resolved.Item,
                            action => _host.RunOnUi(action));

                        _host.NotifyRecordUpdated(job.MovieId);
                        Interlocked.Increment(ref _batchApplied);
                        break;
                    }
                    case DmmResolveOutcome.NoProductCode:
                        Interlocked.Increment(ref _batchNoCode);
                        break;
                    case DmmResolveOutcome.NotFound:
                        Interlocked.Increment(ref _batchNotFound);
                        break;
                    case DmmResolveOutcome.Ambiguous:
                        DmmPendingCandidateStore.Save(
                            job.DbPath,
                            job.MovieId,
                            resolveName,
                            resolved.InitialKeyword ?? DmmInitialKeyword.FromMovieName(resolveName),
                            resolved.Candidates,
                            string.IsNullOrWhiteSpace(job.Source) ? "auto" : job.Source);
                        _host.NotifyPendingCandidatesChanged();
                        Interlocked.Increment(ref _batchAmbiguous);
                        break;
                    default:
                        Interlocked.Increment(ref _batchErrors);
                        break;
                }
            }
            catch (OperationCanceledException) when (!appToken.IsCancellationRequested)
            {
                _batchCancelled = true;
                ClearPendingJobs();
            }
            catch (ObjectDisposedException)
            {
                _batchCancelled = true;
                ClearPendingJobs();
            }
            catch
            {
                Interlocked.Increment(ref _batchErrors);
            }
            finally
            {
                int done = Interlocked.Increment(ref _batchDone);
                await ReportProgressAsync(done, reportPath).ConfigureAwait(false);
            }
        }

        private async Task ReportProgressAsync(int done, string detail)
        {
            DmmFetchProgressSession session = _session;
            if (session == null)
            {
                return;
            }

            await _host.RunOnUiAsync(() => session.Report(done, detail)).ConfigureAwait(false);
        }

        private static string ResolveMovieName(DmmAutoFetchJob job, MovieRecords rec)
        {
            if (!string.IsNullOrWhiteSpace(job.MovieName))
            {
                return job.MovieName;
            }

            if (rec != null)
            {
                if (!string.IsNullOrWhiteSpace(rec.Movie_Path))
                {
                    return System.IO.Path.GetFileName(rec.Movie_Path);
                }

                if (!string.IsNullOrWhiteSpace(rec.Movie_Name))
                {
                    return rec.Movie_Name;
                }
            }

            return string.Empty;
        }

        private void ClearPendingJobs()
        {
            lock (_sync)
            {
                _queue.Clear();
                _pendingIds.Clear();
            }
        }

        private async Task FinishBatchIfIdleAsync()
        {
            bool includesBulk;
            bool cancelled;
            int applied;
            int skipped;
            int noCode;
            int notFound;
            int ambiguous;
            int errors;

            lock (_sync)
            {
                if (_queue.Count > 0 || _session == null)
                {
                    return;
                }

                includesBulk = _batchIncludesBulk;
                cancelled = _batchCancelled;
                applied = _batchApplied;
                skipped = _batchSkipped;
                noCode = _batchNoCode;
                notFound = _batchNotFound;
                ambiguous = _batchAmbiguous;
                errors = _batchErrors;
            }

            // 進捗スロットを先に閉じないと、完了メッセージが「他進捗中」扱いで握りつぶされる。
            await EndSessionAsync().ConfigureAwait(false);

            string label = includesBulk ? "DMM一括取得" : "DMM自動取得";
            string summary = cancelled
                ? $"{label}: キャンセルしました（成功{applied} スキップ{skipped} 品番なし{noCode} 未ヒット{notFound} 未確定保留{ambiguous} エラー{errors}）"
                : $"{label}: 成功{applied} スキップ{skipped} 品番なし{noCode} 未ヒット{notFound} 未確定保留{ambiguous} エラー{errors}";

            _host.ShowCompletionMessage(summary);
            if (includesBulk)
            {
                _host.ShowCompletionDialog(
                    "DMM 情報を一括取得",
                    summary + "\n\nPowered by FANZA Webサービス");
            }
        }

        private async Task EndSessionAsync()
        {
            DmmFetchProgressSession session = _session;
            _session = null;
            if (session == null)
            {
                return;
            }

            await _host.RunOnUiAsync(session.Dispose).ConfigureAwait(false);
        }

        private static bool IsBulkSource(string source) =>
            string.Equals(source, "bulk", StringComparison.OrdinalIgnoreCase);
    }
}
