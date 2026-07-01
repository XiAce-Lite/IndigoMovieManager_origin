using System.Diagnostics;
using System.IO;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    internal sealed class ThumbnailCreationHost
    {
        public string DbFullPath { get; init; }
        public string DbName { get; init; }
        public string ThumbFolder { get; init; }
        public ThumbnailLayoutCache LayoutCache { get; init; }
        public Action<Action> RunOnUi { get; init; }
        public Action<QueueObj, string> ApplyThumbPathsOnUi { get; init; }
        public Action<QueueObj, string> ApplyFailurePlaceholder { get; init; }
        public Action<string, long, object> UpdateMovieColumn { get; init; }
        public Func<bool> IsSessionActive { get; init; }
        public Func<long, MovieRecords> FindMovieRecord { get; init; }
    }

    internal static class ThumbnailCreationOrchestrator
    {
        public static async Task CreateAsync(
            ThumbnailCreationHost host,
            QueueObj queueObj,
            bool isManual = false,
            CancellationToken cts = default)
        {
            if (host == null || queueObj == null || !IsSessionActive(host))
            {
                return;
            }

            MovieRecords movieForProgress = host.FindMovieRecord?.Invoke(queueObj.MovieId);
            queueObj.LastThumbProgressDetail = ThumbnailProgressDetailFormatter.Format(
                queueObj.MovieFullPath,
                null,
                movieForProgress?.Video);

            TabInfo tbi = queueObj.ThumbnailLayout != null
                ? new TabInfo(queueObj.ThumbnailLayout, host.DbName, host.ThumbFolder)
                : new TabInfo(queueObj.Tabindex, host.DbName, host.ThumbFolder);
            string movieFullPath = MediaPathNormalizer.Normalize(queueObj.MovieFullPath);
            string hash = GetHashCRC32(movieFullPath);
            string fileBody = Path.GetFileNameWithoutExtension(movieFullPath).ToLowerInvariant();
            string saveThumbFileName = queueObj.ThumbnailLayout != null && host.LayoutCache != null
                ? host.LayoutCache.GetExpectedThumbPath(queueObj.ThumbnailLayout, fileBody, hash)
                : host.LayoutCache != null
                    ? host.LayoutCache.GetExpectedThumbPath(queueObj.Tabindex, fileBody, hash)
                    : Path.Combine(tbi.OutPath, ThumbnailLayoutCache.GetThumbFileName(fileBody, hash));

            if (isManual && !Path.Exists(saveThumbFileName))
            {
                return;
            }

            if (isManual && ZipMediaKind.IsZipPath(movieFullPath))
            {
                return;
            }

            using IDisposable writeLock = await ThumbnailWriteLock
                .AcquireAsync(saveThumbFileName, cts)
                .ConfigureAwait(false);

            if (!IsSessionActive(host))
            {
                return;
            }

            string tempFileBody = $"{fileBody}_{hash}_tab{queueObj.Tabindex}_temp";
            string tempPath = ApplicationPaths.TempDirectory;

            if (!Path.Exists(tbi.OutPath))
            {
                Directory.CreateDirectory(tbi.OutPath);
            }

            if (!Path.Exists(movieFullPath))
            {
                if (!Path.Exists(saveThumbFileName))
                {
                    string noFileJpeg = ApplicationPaths.ImagesDirectory;
                    noFileJpeg = queueObj.Tabindex switch
                    {
                        0 => Path.Combine(noFileJpeg, "noFileSmall.jpg"),
                        1 => Path.Combine(noFileJpeg, "noFileBig.jpg"),
                        2 => Path.Combine(noFileJpeg, "noFileGrid.jpg"),
                        3 => Path.Combine(noFileJpeg, "noFileList.jpg"),
                        4 => Path.Combine(noFileJpeg, "noFileBig.jpg"),
                        SkinTabIndexHelper.WpfSkinThumbnailSlotIndex => Path.Combine(noFileJpeg, "noFileGrid.jpg"),
                        99 => Path.Combine(noFileJpeg, "noFileGrid.jpg"),
                        _ => Path.Combine(noFileJpeg, "noFileSmall.jpg"),
                    };
                    File.Copy(noFileJpeg, saveThumbFileName, true);
                }

                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: false);
                return;
            }

            ThumbnailJobContext ctx = new()
            {
                QueueObj = queueObj,
                TabInfo = tbi,
                MovieFullPath = movieFullPath,
                SaveThumbFileName = saveThumbFileName,
                TempFileBody = tempFileBody,
                TempPath = tempPath,
                Hash = hash,
                IsManual = isManual,
            };

            if (ZipMediaKind.IsZipPath(movieFullPath))
            {
                if (ZipImageCatalog.TryGetImageEntries(movieFullPath, out IReadOnlyList<string> zipEntries)
                    && zipEntries.Count > 0)
                {
                    MovieRecords zipMovieItem = host.FindMovieRecord?.Invoke(queueObj.MovieId);
                    if (zipMovieItem != null && IsSessionActive(host))
                    {
                        string lengthDisplay = $"{zipEntries.Count}枚";
                        if (zipMovieItem.Movie_Length != lengthDisplay)
                        {
                            string dbPath = host.DbFullPath;
                            long movieId = queueObj.MovieId;
                            long imageCount = zipEntries.Count;
                            host.RunOnUi(() =>
                            {
                                MovieRecords current = host.FindMovieRecord?.Invoke(movieId);
                                if (current != null && current.Movie_Length != lengthDisplay)
                                {
                                    current.Movie_Length = lengthDisplay;
                                }
                            });
                            host.UpdateMovieColumn(dbPath, movieId, imageCount);
                        }
                    }
                }

                ThumbnailCreateResult zipResult = await TryCreateZipThumbnailAsync(ctx, cts).ConfigureAwait(false);
                UpdateThumbProgressDetail(queueObj, zipResult, host.FindMovieRecord?.Invoke(queueObj.MovieId));
                if (!IsSessionActive(host) || host.FindMovieRecord?.Invoke(queueObj.MovieId) == null)
                {
                    return;
                }

                if (zipResult.Success)
                {
                    ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: false);
                    await EnsureDetailThumbnailAfterPrimaryAsync(host, queueObj, saveThumbFileName, cts).ConfigureAwait(false);
                }
                else
                {
                    ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: true);
                }

                return;
            }

            MovieRecords movieItem = host.FindMovieRecord?.Invoke(queueObj.MovieId);
            long knownDurationSec = MovieFileInfoHelper.GetMovieLengthSeconds(movieItem);
            if (!ThumbnailDurationResolver.TryResolve(movieFullPath, out double durationSec, knownDurationSec))
            {
                durationSec = 0;
            }

            if (movieItem != null && durationSec > 0 && IsSessionActive(host))
            {
                string tSpan = new TimeSpan(0, 0, (int)(long)durationSec).ToString(@"hh\:mm\:ss");
                if (movieItem.Movie_Length != tSpan)
                {
                    string dbPath = host.DbFullPath;
                    long movieId = queueObj.MovieId;
                    host.RunOnUi(() =>
                    {
                        MovieRecords current = host.FindMovieRecord?.Invoke(movieId);
                        if (current != null && current.Movie_Length != tSpan)
                        {
                            current.Movie_Length = tSpan;
                        }
                    });
                    host.UpdateMovieColumn(dbPath, movieId, durationSec);
                }
            }

            if (!ThumbnailJobPreparer.TryBuildThumbInfo(ctx, durationSec, out ThumbInfo thumbInfo))
            {
                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: true);
                return;
            }

            bool forceFfmpeg = FfmpegPathResolver.IsForceFfmpegEnabled() && !isManual;
            bool tryOnePassFirst = FfmpegPathResolver.IsOnePassEngineRequested() && !isManual;
            bool created = false;
            ThumbnailCreateResult lastResult = null;

            if (tryOnePassFirst
                && FfmpegPathResolver.TryResolve(out string ffmpegOnePassPath)
                && FfmpegOnePassPolicy.CanUse(thumbInfo, durationSec))
            {
                ThumbnailCreateResult onePassResult = await FfmpegOnePassCreator
                    .TryCreateAsync(ctx, thumbInfo, durationSec, ffmpegOnePassPath, cts)
                    .ConfigureAwait(false);

                lastResult = onePassResult;
                created = onePassResult.Success;
                if (!created)
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffmpeg1pass: {onePassResult.FailureReason}"
                    );
                }
            }

            if (!created && !forceFfmpeg)
            {
                ThumbnailCreateResult openCvResult = await TryCreateWithOpenCvAsync(
                        ctx,
                        thumbInfo,
                        saveThumbFileName,
                        cts)
                    .ConfigureAwait(false);

                lastResult = openCvResult;
                if (openCvResult.Success)
                {
                    created = true;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(openCvResult.FailureReason))
                    {
                        Debug.WriteLine(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] opencv: {openCvResult.FailureReason}"
                        );
                    }

                    ThumbnailMetadataWriter.CleanupPartialOutput(saveThumbFileName, openCvResult.PanelPaths);

                    if (FfmpegPathResolver.IsFallbackEnabled()
                        && FfmpegPathResolver.TryResolve(out string ffmpegPath))
                    {
                        ThumbnailCreateResult ffResult = await FfmpegFallbackCreator
                            .TryCreateAsync(ctx, thumbInfo, durationSec, ffmpegPath, cts)
                            .ConfigureAwait(false);
                        lastResult = ffResult;
                        created = ffResult.Success;
                        if (!created)
                        {
                            Debug.WriteLine(
                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffmpeg: {ffResult.FailureReason}"
                            );
                        }
                    }
                }
            }
            else if (!created && forceFfmpeg && FfmpegPathResolver.TryResolve(out string ffmpegPathForced))
            {
                ThumbnailCreateResult ffResult = await FfmpegFallbackCreator
                    .TryCreateAsync(ctx, thumbInfo, durationSec, ffmpegPathForced, cts)
                    .ConfigureAwait(false);
                lastResult = ffResult;
                created = ffResult.Success;
                if (!created)
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffmpeg(forced): {ffResult.FailureReason}"
                    );
                }
            }

            UpdateThumbProgressDetail(queueObj, lastResult, movieItem);

            if (!IsSessionActive(host) || host.FindMovieRecord?.Invoke(queueObj.MovieId) == null)
            {
                return;
            }

            if (created)
            {
                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: false);
                await EnsureDetailThumbnailAfterPrimaryAsync(host, queueObj, saveThumbFileName, cts).ConfigureAwait(false);
            }
            else
            {
                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: true);
            }
        }

        private static async Task EnsureDetailThumbnailAfterPrimaryAsync(
            ThumbnailCreationHost host,
            QueueObj sourceObj,
            string primaryThumbPath,
            CancellationToken cts)
        {
            if (sourceObj == null
                || sourceObj.Tabindex is < 0 or > 4
                || !IsSessionActive(host)
                || string.IsNullOrWhiteSpace(sourceObj.MovieFullPath))
            {
                return;
            }

            string hash = GetHashCRC32(sourceObj.MovieFullPath);
            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            string fileBody = Path.GetFileNameWithoutExtension(sourceObj.MovieFullPath).ToLowerInvariant();
            string detailPath = host.LayoutCache != null
                ? host.LayoutCache.GetExpectedThumbPath(99, fileBody, hash)
                : Path.Combine(new TabInfo(99, host.DbName, host.ThumbFolder).OutPath,
                    ThumbnailLayoutCache.GetThumbFileName(fileBody, hash));

            var detailObj = new QueueObj
            {
                MovieId = sourceObj.MovieId,
                MovieFullPath = sourceObj.MovieFullPath,
                Tabindex = 99,
                DbFullPath = sourceObj.DbFullPath,
                WorkGeneration = sourceObj.WorkGeneration,
            };

            if (ZipMediaKind.IsZipPath(sourceObj.MovieFullPath))
            {
                bool copied = ZipDetailThumbnailMaterializer.TryCopyFile(primaryThumbPath, detailPath);
                if (!copied
                    && host.LayoutCache != null)
                {
                    copied = ZipDetailThumbnailMaterializer.TryCopyFromExistingTabThumbs(
                        host.LayoutCache,
                        fileBody,
                        hash,
                        detailPath);
                }

                if (copied)
                {
                    ApplyIfAllowed(host, detailObj, detailPath, isFailurePlaceholder: false);
                    return;
                }
            }

            if (File.Exists(detailPath))
            {
                ApplyIfAllowed(host, detailObj, detailPath, isFailurePlaceholder: false);
                return;
            }

            await CreateAsync(host, detailObj, isManual: false, cts).ConfigureAwait(false);
        }

        private static bool IsSessionActive(ThumbnailCreationHost host) =>
            host.IsSessionActive?.Invoke() != false;

        private static void ApplyIfAllowed(
            ThumbnailCreationHost host,
            QueueObj queueObj,
            string saveThumbFileName,
            bool isFailurePlaceholder)
        {
            if (!IsSessionActive(host) || host.FindMovieRecord?.Invoke(queueObj.MovieId) == null)
            {
                return;
            }

            if (isFailurePlaceholder)
            {
                host.ApplyFailurePlaceholder(queueObj, saveThumbFileName);
            }
            else
            {
                host.ApplyThumbPathsOnUi(queueObj, saveThumbFileName);
            }
        }

        private static async Task<ThumbnailCreateResult> TryCreateWithOpenCvAsync(
            ThumbnailJobContext ctx,
            ThumbInfo thumbInfo,
            string saveThumbFileName,
            CancellationToken cts
        )
        {
            ThumbnailCreateResult openCvResult = await OpenCvThumbnailCreator
                .TryCreateAsync(ctx, thumbInfo, cts)
                .ConfigureAwait(false);

            if (openCvResult.Success
                && ThumbnailDuplicateDetector.HasDuplicatePanels(openCvResult.PanelPaths))
            {
                ThumbnailMetadataWriter.CleanupPartialOutput(saveThumbFileName, openCvResult.PanelPaths);

                if (ThumbnailDuplicateRetryPolicy.ShouldRetryOpenCvPerPanel(ctx.IsManual))
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] duplicate panels detected, retrying per-panel opencv"
                    );

                    openCvResult = await OpenCvThumbnailCreator
                        .TryCreatePerPanelAsync(ctx, thumbInfo, cts)
                        .ConfigureAwait(false);
                }
                else
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] duplicate panels detected, deferring to ffmpeg fallback"
                    );

                    return ThumbnailCreateResult.Failed(
                        "opencv duplicate panels",
                        "OpenCV",
                        "OpenCV");
                }
            }

            if (openCvResult.Success)
            {
#if DEBUG == false
                OpenCvThumbnailCreator.CleanupTempPanels(ctx);
#endif
            }

            if (openCvResult.Success
                && ThumbnailDuplicateDetector.HasDuplicatePanels(openCvResult.PanelPaths))
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] duplicate panels remain after per-panel retry"
                );
                ThumbnailMetadataWriter.CleanupPartialOutput(saveThumbFileName, openCvResult.PanelPaths);
                return ThumbnailCreateResult.Failed(
                    "opencv duplicate panels after per-panel retry",
                    "OpenCV",
                    "OpenCV");
            }

            if (!openCvResult.Success && string.IsNullOrEmpty(openCvResult.Backend))
            {
                return ThumbnailCreateResult.Failed(
                    openCvResult.FailureReason,
                    "OpenCV",
                    "OpenCV");
            }

            return openCvResult;
        }

        private static async Task<ThumbnailCreateResult> TryCreateZipThumbnailAsync(
            ThumbnailJobContext ctx,
            CancellationToken cts)
        {
            if (!ZipImageCatalog.TryGetImageEntries(ctx.MovieFullPath, out IReadOnlyList<string> entries)
                || entries.Count == 0)
            {
                return ThumbnailCreateResult.Failed("zip: no images");
            }

            if (!ThumbnailJobPreparer.TryBuildZipThumbInfo(ctx, entries.Count, out ThumbInfo thumbInfo))
            {
                return ThumbnailCreateResult.Failed("zip: thumb info build failed");
            }

            ThumbnailCreateResult result = await ZipThumbnailCreator
                .TryCreateAsync(ctx, thumbInfo, entries, cts)
                .ConfigureAwait(false);

            if (!result.Success && !string.IsNullOrWhiteSpace(result.FailureReason))
            {
                Debug.WriteLine($"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] zip: {result.FailureReason}");
            }

            return result;
        }

        private static void UpdateThumbProgressDetail(
            QueueObj queueObj,
            ThumbnailCreateResult result,
            MovieRecords movie)
        {
            if (queueObj == null)
            {
                return;
            }

            queueObj.LastThumbProgressDetail = ThumbnailProgressDetailFormatter.Format(
                queueObj.MovieFullPath,
                result,
                movie?.Video);
        }
    }
}
