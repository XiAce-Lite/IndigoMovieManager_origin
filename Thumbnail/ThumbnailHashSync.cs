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
        /// 欠落は投入。error プレースホルダも再投入（長さ一致＋非複合の軽い判定）。
        /// </summary>
        /// <param name="ignorePreferJacket">
        /// true のとき、アクティブ WPF のジャケ優先設定を無視する（他スキン用の先作り向け）。
        /// </param>
        internal static bool ShouldEnqueueAfterHashSync(
            MovieRecords item,
            ThumbnailLayoutSpec layout,
            ThumbnailLayoutCache cache,
            ThumbnailHashSyncContext context,
            bool ignorePreferJacket = false)
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
            if (!ignorePreferJacket
                && WpfSkinSettings.PreferJacket
                && !string.IsNullOrEmpty(DmmJacketUrls.GetFrontUrl(item)))
            {
                return false;
            }

            // ExistsOnly: 既存サムネがあるだけで CRC を回さない（スキン切替の走査を軽く保つ）
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
            if (string.IsNullOrWhiteSpace(expectedPath) || !File.Exists(expectedPath))
            {
                return true;
            }

            // 全バイト比較はしない。error っぽい既存ファイルだけ再投入。
            return IsLikelyErrorPlaceholder(expectedPath, cache);
        }

        /// <summary>
        /// error プレースホルダらしいか（全ファイル読込なし）。noFile は再投入しない。
        /// </summary>
        internal static bool IsLikelyErrorPlaceholder(string thumbPath, ThumbnailLayoutCache cache)
        {
            if (string.IsNullOrWhiteSpace(thumbPath) || !File.Exists(thumbPath) || cache == null)
            {
                return false;
            }

            try
            {
                long length = new FileInfo(thumbPath).Length;
                string noFileTemplate = cache.GetNoFilePath(2);
                if (!string.IsNullOrWhiteSpace(noFileTemplate)
                    && File.Exists(noFileTemplate)
                    && length == new FileInfo(noFileTemplate).Length)
                {
                    return false;
                }

                string errorTemplate = cache.GetErrorPath(2);
                if (!string.IsNullOrWhiteSpace(errorTemplate)
                    && File.Exists(errorTemplate)
                    && length == new FileInfo(errorTemplate).Length
                    && !ThumbnailValidityHelper.LooksLikeCompositeThumbnail(thumbPath))
                {
                    return true;
                }

                // テンプレパスが取れない環境向け: 極端に小さい非複合ファイル
                return length < 4096 && !ThumbnailValidityHelper.LooksLikeCompositeThumbnail(thumbPath);
            }
            catch
            {
                return false;
            }
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
