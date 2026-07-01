using System.Data.SQLite;
using System.IO;
using System.Text.RegularExpressions;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// WhiteBrowser 互換の { } 検索を解釈する。
    /// 先頭が "::" のときは組み込み特殊コマンド、それ以外は SQL の WHERE 句として扱う。
    /// </summary>
    internal static class WhiteBrowserBraceSearch
    {
        // SQL インジェクション/破壊的操作を防ぐためのキーワード（単語単位で判定）。
        private static readonly string[] BannedSqlTokens =
        [
            "INSERT", "UPDATE", "DELETE", "DROP", "ATTACH", "DETACH", "PRAGMA", "ALTER", "CREATE",
            "REPLACE", "VACUUM", "REINDEX",
        ];

        public static bool TryApply(
            IReadOnlyList<MovieRecords> source,
            string inner,
            MovieListFilterContext context,
            out IReadOnlyList<MovieRecords> filtered,
            out string overrideSortId)
        {
            filtered = source;
            overrideSortId = null;

            if (string.IsNullOrWhiteSpace(inner))
            {
                filtered = [];
                return true;
            }

            string normalized = inner.Trim();
            if (normalized.StartsWith("::", StringComparison.Ordinal))
            {
                return TryApplySpecialCommand(
                    source,
                    normalized[2..].Trim(),
                    context,
                    out filtered,
                    out overrideSortId);
            }

            if (!IsSafeWhereClause(normalized))
            {
                filtered = [];
                return true;
            }

            if (string.IsNullOrEmpty(context?.DbFullPath))
            {
                filtered = [];
                return true;
            }

            HashSet<long> ids = QueryMovieIds(context.DbFullPath, normalized);
            filtered = [.. source.Where(x => ids.Contains(x.Movie_Id))];
            return true;
        }

        private static bool TryApplySpecialCommand(
            IReadOnlyList<MovieRecords> source,
            string command,
            MovieListFilterContext context,
            out IReadOnlyList<MovieRecords> filtered,
            out string overrideSortId)
        {
            filtered = source;
            overrideSortId = null;

            switch (command.ToLowerInvariant())
            {
                case "duplication":
                case "duplications":
                    HashSet<string> dupHashes = [.. source
                        .GroupBy(x => x.Hash)
                        .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
                        .Select(g => g.Key)];

                    filtered = [.. source.Where(x => dupHashes.Contains(x.Hash))];
                    overrideSortId = "16";
                    return true;

                case "nofile":
                    filtered = [.. source.Where(x => !File.Exists(x.Movie_Path ?? ""))];
                    return true;

                case "error":
                    ThumbnailLayoutCache cache = context?.ThumbnailCache;
                    if (cache == null)
                    {
                        filtered = [];
                    }
                    else
                    {
                        SkinEngine engine = context.CurrentSkinEngine;
                        filtered = [.. source.Where(x =>
                            ThumbnailTabErrorDetector.IsErrorForEngine(x, engine, cache))];
                    }

                    return true;

                default:
                    filtered = [];
                    return true;
            }
        }

        private static bool IsSafeWhereClause(string clause)
        {
            if (string.IsNullOrWhiteSpace(clause))
            {
                return false;
            }

            if (clause.Contains(';', StringComparison.Ordinal)
                || clause.Contains("--", StringComparison.Ordinal)
                || clause.Contains("/*", StringComparison.Ordinal))
            {
                return false;
            }

            // 単語単位で禁止語を判定する（create_time のような列名を誤検知しないため）。
            foreach (string token in BannedSqlTokens)
            {
                if (Regex.IsMatch(clause, $@"\b{token}\b", RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static HashSet<long> QueryMovieIds(string dbFullPath, string whereClause)
        {
            HashSet<long> ids = [];
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT movie_id FROM movie WHERE ({whereClause})";
                using SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                    {
                        ids.Add(Convert.ToInt64(reader[0]));
                    }
                }
            }
            catch
            {
                return [];
            }

            return ids;
        }
    }
}
