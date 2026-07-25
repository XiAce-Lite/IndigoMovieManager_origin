using System.Collections.Concurrent;
using System.IO;
using IndigoMovieManager.Services.Dmm;
using IndigoMovieManager.Services.WpfSkin;
using static IndigoMovieManager.SQLite;
using static IndigoMovieManager.Tools;

namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// DB の movie.hash と実ファイル hash を揃え、サムネパス解決の食い違いを防ぐ。
    /// </summary>
    internal static class ThumbnailHashSync
    {
        internal enum ThumbPathSatisfactionMode
        {
            /// <summary>キュー投入判定: ファイルが存在すれば足りる。</summary>
            ExistsOnly,
            /// <summary>エラー判定: noFile / 有効 composite は満たす。error / 欠落は未充足。</summary>
            ErrorCheck,
        }

        private static readonly ConcurrentDictionary<string, string> FileHashCache = new();

        public static ThumbnailHashSyncContext ForDatabase(string dbFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath))
            {
                return null;
            }

            return new ThumbnailHashSyncContext
            {
                DbFullPath = dbFullPath,
                ComputeFileHash = ComputeFileHashCached,
                UpdateDbHash = (movieId, hash) =>
                {
                    if (File.Exists(dbFullPath))
                    {
                        UpdateMovieSingleColumn(dbFullPath, movieId, "hash", hash);
                    }
                },
            };
        }

        /// <summary>
        /// 必要なら DB / メモリ上の hash を同期し、サムネ解決に使う hash を返す。
        /// </summary>
        public static string ResolveHashForThumbnail(
            MovieRecords item,
            ThumbnailLayoutSpec layout,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext context,
            ThumbPathSatisfactionMode mode = ThumbPathSatisfactionMode.ExistsOnly)
        {
            if (item == null)
            {
                return "";
            }

            string moviePath = item.Movie_Path ?? "";
            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return NormalizeHash(item.Hash);
            }

            if (layout == null || cache == null)
            {
                return NormalizeHash(item.Hash);
            }

            string fileBody = ThumbnailMovieNaming.GetMovieBody(item);
            string dbHash = NormalizeHash(item.Hash);

            if (!string.IsNullOrEmpty(dbHash))
            {
                string dbExpected = cache.GetExpectedThumbPath(layout, fileBody, dbHash);
                if (IsThumbPathSatisfied(dbExpected, cache, mode))
                {
                    return dbHash;
                }
            }

            if (context == null)
            {
                return dbHash;
            }

            Func<string, string> compute = context.ComputeFileHash ?? ComputeFileHashCached;
            string fileHash = NormalizeHash(compute(moviePath));
            if (string.IsNullOrEmpty(fileHash))
            {
                return dbHash;
            }

            if (!string.IsNullOrEmpty(dbHash)
                && string.Equals(dbHash, fileHash, StringComparison.OrdinalIgnoreCase))
            {
                return dbHash;
            }

            ApplyHashSync(item, fileHash, context);
            return fileHash;
        }

        /// <summary>
        /// ShouldEnqueueTabSwitchWork と同じ判定（hash 同期後）。
        /// </summary>
        internal static bool ShouldEnqueueAfterHashSync(
            MovieRecords item,
            ThumbnailLayoutSpec layout,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext context)
        {
            if (item == null
                || layout == null
                || cache == null
                || string.IsNullOrWhiteSpace(item.Movie_Path)
                || !File.Exists(item.Movie_Path))
            {
                return false;
            }

            // ジャケ写優先スキンで URL がある場合はローカルサムネを作らない
            if (WpfSkinSettings.PreferJacket
                && !string.IsNullOrEmpty(DmmJacketUrls.GetFrontUrl(item)))
            {
                return false;
            }

            ResolveHashForThumbnail(
                item,
                layout,
                cache,
                context,
                ThumbPathSatisfactionMode.ExistsOnly);

            if (string.IsNullOrWhiteSpace(item.Hash))
            {
                return false;
            }

            string fileBody = ThumbnailMovieNaming.GetMovieBody(item);
            string expectedPath = cache.GetExpectedThumbPath(layout, fileBody, item.Hash);
            return string.IsNullOrWhiteSpace(expectedPath) || !File.Exists(expectedPath);
        }

        internal static void ClearFileHashCache() => FileHashCache.Clear();

        private static void ApplyHashSync(
            MovieRecords item,
            string fileHash,
            ThumbnailHashSyncContext context)
        {
            item.Hash = fileHash;
            context.UpdateDbHash?.Invoke(item.Movie_Id, fileHash);
        }

        private static string NormalizeHash(string hash) =>
            string.IsNullOrWhiteSpace(hash) ? "" : hash.Trim().ToLowerInvariant();

        private static string ComputeFileHashCached(string moviePath)
        {
            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return "";
            }

            long lastWriteTicks = new FileInfo(moviePath).LastWriteTimeUtc.Ticks;
            string cacheKey = $"{moviePath}|{lastWriteTicks}";
            return FileHashCache.GetOrAdd(cacheKey, _ => GetHashCRC32(moviePath) ?? "");
        }

        private static bool IsThumbPathSatisfied(
            string expectedPath,
            ThumbnailLayoutCache cache,
            ThumbPathSatisfactionMode mode)
        {
            if (string.IsNullOrWhiteSpace(expectedPath) || !File.Exists(expectedPath))
            {
                return false;
            }

            if (mode == ThumbPathSatisfactionMode.ExistsOnly)
            {
                return true;
            }

            string noFileTemplate = cache.GetNoFilePath(2);
            if (PlaceholderFilesMatch(expectedPath, noFileTemplate))
            {
                return true;
            }

            string errorTemplate = cache.GetErrorPath(2);
            if (PlaceholderFilesMatch(expectedPath, errorTemplate))
            {
                return false;
            }

            return ThumbnailValidityHelper.LooksLikeCompositeThumbnail(expectedPath);
        }

        private static bool PlaceholderFilesMatch(string filePath, string templatePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(templatePath))
            {
                return false;
            }

            if (!File.Exists(filePath) || !File.Exists(templatePath))
            {
                return false;
            }

            FileInfo candidate = new(filePath);
            FileInfo template = new(templatePath);
            if (candidate.Length != template.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> candidateBytes = File.ReadAllBytes(filePath);
            ReadOnlySpan<byte> templateBytes = File.ReadAllBytes(templatePath);
            return candidateBytes.SequenceEqual(templateBytes);
        }
    }
}
