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
        public IEnumerable<MovieRecords> MovieRecords { get; init; }
        public ThumbnailLayoutCache LayoutCache { get; init; }
        public Action<Action> RunOnUi { get; init; }
        public Action<QueueObj, string> ApplyThumbPathsOnUi { get; init; }
        public Action<QueueObj, string> ApplyFailurePlaceholder { get; init; }
        public bool IsResizeThumb { get; init; }
        public Action<string, long, object> UpdateMovieColumn { get; init; }
    }

    internal static class ThumbnailCreationOrchestrator
    {
        public static async Task CreateAsync(
            ThumbnailCreationHost host,
            QueueObj queueObj,
            bool isManual = false,
            CancellationToken cts = default)
        {
            TabInfo tbi = new(queueObj.Tabindex, host.DbName, host.ThumbFolder);
            string movieFullPath = queueObj.MovieFullPath;
            string hash = GetHashCRC32(movieFullPath);
            string fileBody = Path.GetFileNameWithoutExtension(movieFullPath);
            string saveThumbFileName = Path.Combine(tbi.OutPath, $"{fileBody}.#{hash}.jpg");

            if (isManual && !Path.Exists(saveThumbFileName))
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

                host.ApplyThumbPathsOnUi(queueObj, saveThumbFileName);
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

            MovieRecords movieItem = host.MovieRecords.FirstOrDefault(x => x.Movie_Id == queueObj.MovieId);
            if (movieItem != null && durationSec > 0)
            {
                string tSpan = new TimeSpan(0, 0, (int)(long)durationSec).ToString(@"hh\:mm\:ss");
                if (movieItem.Movie_Length != tSpan)
                {
                    string dbPath = host.DbFullPath;
                    long movieId = queueObj.MovieId;
                    host.RunOnUi(() =>
                    {
                        if (movieItem.Movie_Length != tSpan)
                        {
                            movieItem.Movie_Length = tSpan;
                        }
                    });
                    host.UpdateMovieColumn(dbPath, movieId, durationSec);
                }
            }

            if (!ThumbnailJobPreparer.TryBuildThumbInfo(ctx, durationSec, out ThumbInfo thumbInfo))
            {
                host.ApplyFailurePlaceholder(queueObj, saveThumbFileName);
                return;
            }

            bool forceFfmpeg = FfmpegPathResolver.IsForceFfmpegEnabled() && !isManual;
            bool created = false;

            if (!forceFfmpeg)
            {
                ThumbnailCreateResult openCvResult = await OpenCvThumbnailCreator
                    .TryCreateAsync(ctx, thumbInfo, cts)
                    .ConfigureAwait(false);

                if (openCvResult.Success)
                {
                    if (ThumbnailDuplicateDetector.HasDuplicatePanels(openCvResult.PanelPaths))
                    {
                        Debug.WriteLine(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [thumb] duplicate panels accepted (opencv)"
                        );
                    }

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

            if (created)
            {
                host.ApplyThumbPathsOnUi(queueObj, saveThumbFileName);
            }
            else
            {
                host.ApplyFailurePlaceholder(queueObj, saveThumbFileName);
            }
        }
    }
}
