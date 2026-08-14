using System.Diagnostics;
using System.Globalization;
using System.Text;
using IndigoMovieManager.Services;
using IndigoMovieManager.Thumbnail;
using Xunit;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Tests;

/// <summary>
/// スキン相当レイアウト（DefaultSmall / DefaultBig10 等）で旧経路と新経路を比較し CSV 出力する。
/// 環境変数 IMM_THUMB_BENCH_RUN=1 と IMM_THUMB_BENCH_ROOT が必要。
/// </summary>
[CollectionDefinition("ThumbnailCompareBench", DisableParallelization = true)]
public sealed class ThumbnailCompareBenchCollection;

[Collection("ThumbnailCompareBench")]
public sealed class ThumbnailCompareBenchTests
{
    private const string RunEnvName = "IMM_THUMB_BENCH_RUN";
    private const string RootEnvName = "IMM_THUMB_BENCH_ROOT";
    private const string CsvEnvName = "IMM_THUMB_BENCH_CSV";
    private const string ThumbRootEnvName = "IMM_THUMB_BENCH_THUMB_ROOT";
    private const string MaxFilesEnvName = "IMM_THUMB_BENCH_MAX_FILES";
    private const string ParallelismEnvName = "IMM_THUMB_BENCH_PARALLELISM";
    private const string LayoutEnvName = "IMM_THUMB_BENCH_LAYOUT";
    private const string DbName = "test";

    private static readonly long Gib = 1024L * 1024 * 1024;

    [Fact]
    public async Task Compare_old_vs_new_thumbnail_engine()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunEnvName)?.Trim(), "1"))
        {
            return;
        }

        string root = Environment.GetEnvironmentVariable(RootEnvName)?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        string csvPath = Environment.GetEnvironmentVariable(CsvEnvName)?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            csvPath = Path.Combine(Path.GetTempPath(), "imm-thumb-bench.csv");
        }

        string thumbRoot = Environment.GetEnvironmentVariable(ThumbRootEnvName)?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(thumbRoot))
        {
            thumbRoot = Path.Combine(Path.GetDirectoryName(csvPath) ?? Path.GetTempPath(), "thumbs");
        }

        int maxFiles = ResolveIntEnv(MaxFilesEnvName, defaultValue: 50, min: 1, max: 500);
        int benchParallelism = ResolveIntEnv(ParallelismEnvName, defaultValue: 8, min: 1, max: 32);
        ThumbnailLayoutSpec layout = ResolveBenchLayout();

        Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? ".");
        Directory.CreateDirectory(thumbRoot);

        IReadOnlyList<string> videoPaths = DiscoverBenchmarkVideos(root, maxFiles);
        if (videoPaths.Count == 0)
        {
            return;
        }

        int savedParallelism = Properties.Settings.Default.ThumbnailParallelism;
        string savedHwMode = Properties.Settings.Default.FfmpegHardwareDecodeMode ?? "Off";

        Properties.Settings.Default.ThumbnailParallelism = benchParallelism;
        Properties.Settings.Default.FfmpegHardwareDecodeMode = "Auto";
        FfmpegHardwareDecodePolicy.InvalidateCache();

        var cache = new ThumbnailLayoutCache();
        cache.Refresh(DbName, thumbRoot);

        string dbFullPath = Path.Combine(Path.GetDirectoryName(csvPath) ?? thumbRoot, "test.wb");

        var fileMeta = new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
        long movieId = 0;
        foreach (string videoPath in videoPaths)
        {
            movieId++;
            fileMeta[videoPath] = BuildFileMeta(videoPath, movieId);
        }

        long totalOldMs = 0;
        long totalNewMs = 0;
        int oldSuccessCount = 0;
        int newSuccessCount = 0;

        Dictionary<string, BenchRun> oldRuns = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, BenchRun> newRuns = new(StringComparer.OrdinalIgnoreCase);

        Stopwatch wallOld = Stopwatch.StartNew();
        foreach (string videoPath in videoPaths)
        {
            FileMeta meta = fileMeta[videoPath];
            BenchRun run = await RunEnginePassAsync(
                cache,
                dbFullPath,
                thumbRoot,
                layout,
                meta,
                useOpenCvFirst: true).ConfigureAwait(false);
            oldRuns[videoPath] = run;
            if (run.Success)
            {
                totalOldMs += run.ElapsedMs;
                oldSuccessCount++;
            }

            Debug.WriteLine($"[thumb-bench][old] {meta.FileName} {run.ElapsedMs}ms {run.Backend}");
        }
        wallOld.Stop();

        Stopwatch wallNew = Stopwatch.StartNew();
        foreach (string videoPath in videoPaths)
        {
            FileMeta meta = fileMeta[videoPath];
            BenchRun run = await RunEnginePassAsync(
                cache,
                dbFullPath,
                thumbRoot,
                layout,
                meta,
                useOpenCvFirst: false).ConfigureAwait(false);
            newRuns[videoPath] = run;
            if (run.Success)
            {
                totalNewMs += run.ElapsedMs;
                newSuccessCount++;
            }

            Debug.WriteLine($"[thumb-bench][new] {meta.FileName} {run.ElapsedMs}ms {run.Backend}");
        }
        wallNew.Stop();

        try
        {
            await using var writer = new StreamWriter(csvPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            await writer.WriteLineAsync(
                "FileName,FileSizeBytes,FileSizeGB,Resolution,DurationSec,OldMs,NewMs,DeltaMs,Speedup,OldBackend,NewBackend,OldSuccess,NewSuccess");

            foreach (string videoPath in videoPaths)
            {
                FileMeta meta = fileMeta[videoPath];
                oldRuns.TryGetValue(videoPath, out BenchRun oldRun);
                newRuns.TryGetValue(videoPath, out BenchRun newRun);
                var row = BenchRow.From(meta, oldRun, newRun);
                await writer.WriteLineAsync(row.ToCsvLine());
                await writer.FlushAsync();
            }

            await writer.WriteLineAsync(
                $"TOTAL,,,,,{totalOldMs},{totalNewMs},{totalNewMs - totalOldMs},{FormatSpeedup(totalOldMs, totalNewMs)},,old_ok={oldSuccessCount},new_ok={newSuccessCount},,");
            await writer.WriteLineAsync(
                $"WALL_OLD_PASS_MS,,,,,{wallOld.ElapsedMilliseconds},,,,,,,,");
            await writer.WriteLineAsync(
                $"WALL_NEW_PASS_MS,,,,,,,{wallNew.ElapsedMilliseconds},,,,,,");
            await writer.WriteLineAsync(
                $"CONFIG,,,,parallelism={benchParallelism},hw=Auto,maxFiles={maxFiles},layout={layout.Key},skin={ResolveLayoutName()},,,,,,");
        }
        finally
        {
            Properties.Settings.Default.ThumbnailParallelism = savedParallelism;
            Properties.Settings.Default.FfmpegHardwareDecodeMode = savedHwMode;
            FfmpegHardwareDecodePolicy.InvalidateCache();
        }

        Assert.True(File.Exists(csvPath));
        Debug.WriteLine(
            $"[thumb-bench] CSV written: {csvPath} files={videoPaths.Count} old={wallOld.ElapsedMilliseconds}ms new={wallNew.ElapsedMilliseconds}ms");
    }

    internal static ThumbnailLayoutSpec ResolveBenchLayout() => ResolveLayoutSpec(ResolveLayoutName());

    internal static string ResolveLayoutName()
    {
        string raw = Environment.GetEnvironmentVariable(LayoutEnvName)?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(raw) ? "DefaultBig10" : raw;
    }

    internal static ThumbnailLayoutSpec ResolveLayoutSpec(string layoutName)
    {
        if (string.Equals(layoutName, "DefaultSmall", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(120, 90, 3, 1);
        }

        if (string.Equals(layoutName, "DefaultBig10", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(120, 90, 5, 2);
        }

        if (string.Equals(layoutName, "Bench5", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(120, 90, 5, 1);
        }

        if (string.Equals(layoutName, "Bench4", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(120, 90, 4, 1);
        }

        if (string.Equals(layoutName, "Bench4x3", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(360, 270, 4, 1);
        }

        if (string.Equals(layoutName, "Bench5x3", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(360, 270, 5, 1);
        }

        if (string.Equals(layoutName, "Bench7", StringComparison.OrdinalIgnoreCase))
        {
            return new ThumbnailLayoutSpec(120, 90, 7, 1);
        }

        throw new InvalidOperationException($"unsupported bench layout: {layoutName}");
    }

    /// <summary>テスト用: 5GB→4GB→3GB の順で最大件数まで選ぶ。</summary>
    internal static IReadOnlyList<string> DiscoverBenchmarkVideos(string root, int maxFiles)
    {
        string checkExt = Properties.Settings.Default.CheckExt ?? "";
        List<(string Path, long Size)> candidates = [];

        foreach (string path in Directory.EnumerateFiles(root))
        {
            if (ZipMediaKind.IsZipPath(path))
            {
                continue;
            }

            if (!MediaExtensionSettings.ShouldScanFile(path, checkExt, excludeExtSetting: null))
            {
                continue;
            }

            long size = 0;
            try
            {
                size = new FileInfo(path).Length;
            }
            catch
            {
                continue;
            }

            candidates.Add((path, size));
        }

        candidates.Sort((a, b) => b.Size.CompareTo(a.Size));

        List<string> selected = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        long[] thresholds = [5L * Gib, 4L * Gib, 3L * Gib];

        foreach (long threshold in thresholds)
        {
            foreach ((string path, long size) in candidates)
            {
                if (selected.Count >= maxFiles)
                {
                    return selected;
                }

                if (size < threshold || !seen.Add(path))
                {
                    continue;
                }

                selected.Add(path);
            }
        }

        return selected;
    }

    private static int ResolveIntEnv(string name, int defaultValue, int min, int max)
    {
        string raw = Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            parsed = defaultValue;
        }

        if (parsed < min)
        {
            return min;
        }

        if (parsed > max)
        {
            return max;
        }

        return parsed;
    }

    private static FileMeta BuildFileMeta(string videoPath, long movieId)
    {
        var fileInfo = new FileInfo(videoPath);
        ThumbnailDurationResolver.TryResolve(videoPath, out double durationSec, knownDurationSec: 0);

        return new FileMeta
        {
            MovieId = movieId,
            VideoPath = videoPath,
            FileName = Path.GetFileName(videoPath),
            FileBody = Path.GetFileNameWithoutExtension(videoPath).ToLowerInvariant(),
            Hash = GetHashCRC32(videoPath),
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            DurationSec = durationSec,
            Resolution = TryGetVideoResolution(videoPath),
        };
    }

    private static async Task<BenchRun> RunEnginePassAsync(
        ThumbnailLayoutCache cache,
        string dbFullPath,
        string thumbRoot,
        ThumbnailLayoutSpec layout,
        FileMeta meta,
        bool useOpenCvFirst)
    {
        string saveThumb = cache.GetExpectedThumbPath(layout, meta.FileBody, meta.Hash);
        Directory.CreateDirectory(Path.GetDirectoryName(saveThumb) ?? Path.Combine(thumbRoot, layout.Key));

        var movie = new MovieRecords
        {
            Movie_Id = meta.MovieId,
            Movie_Path = meta.VideoPath,
            Movie_Name = meta.FileBody,
            Hash = meta.Hash,
        };

        var host = CreateHost(dbFullPath, thumbRoot, cache, movie);
        var queueObj = new QueueObj
        {
            MovieId = meta.MovieId,
            MovieFullPath = meta.VideoPath,
            ThumbnailLayout = layout,
            DbFullPath = dbFullPath,
            WorkGeneration = 1,
        };

        return await RunOnceAsync(host, queueObj, saveThumb, useOpenCvFirst).ConfigureAwait(false);
    }

    private static ThumbnailCreationHost CreateHost(
        string dbFullPath,
        string thumbRoot,
        ThumbnailLayoutCache cache,
        MovieRecords movie) =>
        new()
        {
            DbFullPath = dbFullPath,
            DbName = DbName,
            ThumbFolder = thumbRoot,
            LayoutCache = cache,
            RunOnUi = action => action(),
            ApplyThumbPathsOnUi = (_, _) => { },
            ApplyFailurePlaceholder = (_, _) => { },
            UpdateMovieColumn = (_, _, _) => { },
            IsSessionActive = () => true,
            FindMovieRecord = id => id == movie.Movie_Id ? movie : null,
        };

    private static async Task<BenchRun> RunOnceAsync(
        ThumbnailCreationHost host,
        QueueObj queueObj,
        string saveThumb,
        bool useOpenCvFirst)
    {
        DeleteIfExists(saveThumb);

        string previousAutoEngine = Environment.GetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE");
        try
        {
            if (useOpenCvFirst)
            {
                Environment.SetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE", "opencv");
            }
            else
            {
                Environment.SetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE", null);
            }

            Stopwatch sw = Stopwatch.StartNew();
            await ThumbnailCreationOrchestrator.CreateAsync(host, queueObj, isManual: false).ConfigureAwait(false);
            sw.Stop();

            bool success = File.Exists(saveThumb)
                && ThumbnailValidityHelper.LooksLikeCompositeThumbnail(saveThumb);
            string backend = ParseBackendLabel(queueObj.LastThumbProgressDetail);

            return new BenchRun(success, sw.ElapsedMilliseconds, backend);
        }
        finally
        {
            Environment.SetEnvironmentVariable("IMM_THUMB_AUTO_ENGINE", previousAutoEngine);
        }
    }

    private static string TryGetVideoResolution(string videoPath)
    {
        if (!FfmpegPathResolver.TryResolveFfprobe(out string ffprobePath))
        {
            return "";
        }

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = ffprobePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-select_streams");
            psi.ArgumentList.Add("v:0");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("stream=width,height");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("csv=p=0:s=x");
            psi.ArgumentList.Add(videoPath);

            using Process process = Process.Start(psi);
            if (process == null)
            {
                return "";
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                return "";
            }

            string line = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
            return line.Contains('x', StringComparison.Ordinal) ? line : "";
        }
        catch
        {
            return "";
        }
    }

    private static string ParseBackendLabel(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return "";
        }

        string[] parts = detail.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[1] : "";
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string FormatSpeedup(long oldMs, long newMs)
    {
        if (oldMs <= 0 || newMs <= 0)
        {
            return "";
        }

        return (oldMs / (double)newMs).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class FileMeta
    {
        public long MovieId { get; init; }
        public string VideoPath { get; init; } = "";
        public string FileName { get; init; } = "";
        public string FileBody { get; init; } = "";
        public string Hash { get; init; } = "";
        public long FileSizeBytes { get; init; }
        public double DurationSec { get; init; }
        public string Resolution { get; init; } = "";
    }

    private sealed class BenchRun(bool success, long elapsedMs, string backend)
    {
        public bool Success { get; } = success;
        public long ElapsedMs { get; } = elapsedMs;
        public string Backend { get; } = backend ?? "";
    }

    private sealed class BenchRow
    {
        public string FileName { get; init; } = "";
        public long FileSizeBytes { get; init; }
        public string Resolution { get; init; } = "";
        public double DurationSec { get; init; }
        public long OldMs { get; init; }
        public long NewMs { get; init; }
        public string OldBackend { get; init; } = "";
        public string NewBackend { get; init; } = "";
        public bool OldSuccess { get; init; }
        public bool NewSuccess { get; init; }

        public static BenchRow From(FileMeta meta, BenchRun oldRun, BenchRun newRun) =>
            new()
            {
                FileName = meta.FileName,
                FileSizeBytes = meta.FileSizeBytes,
                Resolution = meta.Resolution,
                DurationSec = meta.DurationSec,
                OldMs = oldRun?.ElapsedMs ?? 0,
                NewMs = newRun?.ElapsedMs ?? 0,
                OldBackend = oldRun?.Backend ?? "",
                NewBackend = newRun?.Backend ?? "",
                OldSuccess = oldRun?.Success ?? false,
                NewSuccess = newRun?.Success ?? false,
            };

        public string ToCsvLine()
        {
            double sizeGb = FileSizeBytes / (1024d * 1024 * 1024);
            long delta = NewMs - OldMs;
            string speedup = OldSuccess && NewSuccess ? FormatSpeedup(OldMs, NewMs) : "";

            return string.Join(",",
                Csv(FileName),
                FileSizeBytes.ToString(CultureInfo.InvariantCulture),
                sizeGb.ToString("0.###", CultureInfo.InvariantCulture),
                Csv(Resolution),
                DurationSec.ToString("0.###", CultureInfo.InvariantCulture),
                OldMs.ToString(CultureInfo.InvariantCulture),
                NewMs.ToString(CultureInfo.InvariantCulture),
                delta.ToString(CultureInfo.InvariantCulture),
                speedup,
                Csv(OldBackend),
                Csv(NewBackend),
                OldSuccess ? "1" : "0",
                NewSuccess ? "1" : "0");
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
