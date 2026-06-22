using System.Diagnostics;
using System.IO;
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
        public bool IsResizeThumb { get; init; }
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

            TabInfo tbi = new(queueObj.Tabindex, host.DbName, host.ThumbFolder);
            string movieFullPath = queueObj.MovieFullPath;
            string hash = GetHashCRC32(movieFullPath);
            string fileBody = Path.GetFileNameWithoutExtension(movieFullPath);
            string saveThumbFileName = Path.Combine(tbi.OutPath, $"{fileBody}.#{hash}.jpg");

            if (isManual && !Path.Exists(saveThumbFileName))
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
            string tempPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            if (!Path.Exists(tempPath))
            {
                Directory.CreateDirectory(tempPath);
            }

            if (!Path.Exists(tbi.OutPath))
            {
                Directory.CreateDirectory(tbi.OutPath);
            }

            if (!Path.Exists(queueObj.MovieFullPath))
            {
                if (!Path.Exists(saveThumbFileName))
                {
                    string noFileJpeg = Path.Combine(Directory.GetCurrentDirectory(), "Images");
                    noFileJpeg = queueObj.Tabindex switch
                    {
                        0 => Path.Combine(noFileJpeg, "noFileSmall.jpg"),
                        1 => Path.Combine(noFileJpeg, "noFileBig.jpg"),
                        2 => Path.Combine(noFileJpeg, "noFileGrid.jpg"),
                        3 => Path.Combine(noFileJpeg, "noFileList.jpg"),
                        4 => Path.Combine(noFileJpeg, "noFileBig.jpg"),
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
                IsResizeThumb = host.IsResizeThumb,
            };

            if (!ThumbnailDurationResolver.TryResolve(movieFullPath, out double durationSec))
            {
                durationSec = 0;
            }

            MovieRecords movieItem = host.FindMovieRecord?.Invoke(queueObj.MovieId);
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
            bool created = false;

            if (!forceFfmpeg)
            {
                ThumbnailCreateResult openCvResult = await TryCreateWithOpenCvAsync(
                        ctx,
                        thumbInfo,
                        saveThumbFileName,
                        cts)
                    .ConfigureAwait(false);

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
            else if (FfmpegPathResolver.TryResolve(out string ffmpegPathForced))
            {
                ThumbnailCreateResult ffResult = await FfmpegFallbackCreator
                    .TryCreateAsync(ctx, thumbInfo, durationSec, ffmpegPathForced, cts)
                    .ConfigureAwait(false);
                created = ffResult.Success;
                if (!created)
                {
                    Debug.WriteLine(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] ffmpeg(forced): {ffResult.FailureReason}"
                    );
                }
            }

            if (!IsSessionActive(host) || host.FindMovieRecord?.Invoke(queueObj.MovieId) == null)
            {
                return;
            }

            if (created)
            {
                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: false);
            }
            else
            {
                ApplyIfAllowed(host, queueObj, saveThumbFileName, isFailurePlaceholder: true);
            }
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
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] duplicate panels detected, retrying per-panel opencv"
                );
                ThumbnailMetadataWriter.CleanupPartialOutput(saveThumbFileName, openCvResult.PanelPaths);

                openCvResult = await OpenCvThumbnailCreator
                    .TryCreatePerPanelAsync(ctx, thumbInfo, cts)
                    .ConfigureAwait(false);
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
                return ThumbnailCreateResult.Failed("opencv duplicate panels after per-panel retry");
            }

            return openCvResult;
        }
    }
}
