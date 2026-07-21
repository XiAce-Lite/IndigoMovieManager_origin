using System.Data.SQLite;
using System.Text.Json;

namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmPendingCandidateRecord
    {
        public long PendingId { get; init; }
        public long MovieId { get; init; }
        public string MovieName { get; init; }
        public string InitialKeyword { get; init; }
        public string Source { get; init; }
        public DateTime CreatedAt { get; init; }
        public IReadOnlyList<DmmCandidateEntry> Candidates { get; init; } = [];
    }

    internal static class DmmPendingCandidateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        public static void EnsureTable(string dbFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath))
            {
                return;
            }

            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS dmm_pending(
                    pending_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    movie_id INTEGER NOT NULL,
                    movie_name TEXT NOT NULL DEFAULT '',
                    initial_keyword TEXT NOT NULL DEFAULT '',
                    candidates_json TEXT NOT NULL DEFAULT '[]',
                    source TEXT NOT NULL DEFAULT '',
                    created_at DATETIME NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        public static void Save(
            string dbFullPath,
            long movieId,
            string movieName,
            string initialKeyword,
            IReadOnlyList<DmmCandidateEntry> candidates,
            string source)
        {
            EnsureTable(dbFullPath);
            DeleteByMovieId(dbFullPath, movieId);

            string json = JsonSerializer.Serialize(candidates ?? [], JsonOptions);
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO dmm_pending(movie_id, movie_name, initial_keyword, candidates_json, source, created_at)
                VALUES (@movie_id, @movie_name, @initial_keyword, @candidates_json, @source, @created_at)";
            cmd.Parameters.AddWithValue("@movie_id", movieId);
            cmd.Parameters.AddWithValue("@movie_name", movieName ?? string.Empty);
            cmd.Parameters.AddWithValue("@initial_keyword", initialKeyword ?? string.Empty);
            cmd.Parameters.AddWithValue("@candidates_json", json);
            cmd.Parameters.AddWithValue("@source", source ?? string.Empty);
            cmd.Parameters.AddWithValue("@created_at", DateTime.Now);
            cmd.ExecuteNonQuery();
        }

        public static List<DmmPendingCandidateRecord> List(string dbFullPath)
        {
            EnsureTable(dbFullPath);
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT pending_id, movie_id, movie_name, initial_keyword, candidates_json, source, created_at
                FROM dmm_pending
                ORDER BY created_at DESC, pending_id DESC";
            using SQLiteDataReader reader = cmd.ExecuteReader();

            var records = new List<DmmPendingCandidateRecord>();
            while (reader.Read())
            {
                records.Add(Map(reader));
            }

            return records;
        }

        public static void Delete(string dbFullPath, long pendingId)
        {
            EnsureTable(dbFullPath);
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM dmm_pending WHERE pending_id = @pending_id";
            cmd.Parameters.AddWithValue("@pending_id", pendingId);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteByMovieId(string dbFullPath, long movieId)
        {
            EnsureTable(dbFullPath);
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM dmm_pending WHERE movie_id = @movie_id";
            cmd.Parameters.AddWithValue("@movie_id", movieId);
            cmd.ExecuteNonQuery();
        }

        public static int Count(string dbFullPath)
        {
            EnsureTable(dbFullPath);
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM dmm_pending";
            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        /// <summary>
        /// movie テーブルに存在しない movie_id の未確定候補を削除する。
        /// </summary>
        public static int DeleteOrphaned(string dbFullPath)
        {
            EnsureTable(dbFullPath);
            if (!HasMovieTable(dbFullPath))
            {
                return 0;
            }

            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM dmm_pending
                WHERE movie_id NOT IN (SELECT movie_id FROM movie)";
            return cmd.ExecuteNonQuery();
        }

        private static bool HasMovieTable(string dbFullPath)
        {
            using SQLiteConnection connection = new($"Data Source={dbFullPath}");
            connection.Open();
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'movie' LIMIT 1";
            object result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }

        private static DmmPendingCandidateRecord Map(SQLiteDataReader reader)
        {
            string json = reader["candidates_json"]?.ToString() ?? "[]";
            List<DmmCandidateEntry> candidates;
            try
            {
                candidates = JsonSerializer.Deserialize<List<DmmCandidateEntry>>(json, JsonOptions) ?? [];
            }
            catch
            {
                candidates = [];
            }

            return new DmmPendingCandidateRecord
            {
                PendingId = Convert.ToInt64(reader["pending_id"]),
                MovieId = Convert.ToInt64(reader["movie_id"]),
                MovieName = reader["movie_name"]?.ToString() ?? string.Empty,
                InitialKeyword = reader["initial_keyword"]?.ToString() ?? string.Empty,
                Source = reader["source"]?.ToString() ?? string.Empty,
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                Candidates = candidates,
            };
        }
    }
}
