using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using static IndigoMovieManager.SQLite;

namespace IndigoMovieManager.Services
{
    internal static class MovieRegistrationHelper
    {
        public static bool TryCreateMovieInfo(string fileFullPath, out MovieInfo movieInfo, bool noHash = false)
        {
            movieInfo = null;
            string normalizedPath = MediaPathNormalizer.Normalize(fileFullPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !File.Exists(normalizedPath))
            {
                return false;
            }

            try
            {
                movieInfo = new MovieInfo(normalizedPath, noHash);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] MovieInfo failed: {normalizedPath} : {ex.Message}");
                return false;
            }
        }

        public static async Task<MovieInfo> TryRegisterDiscoveredFileAsync(string dbFullPath, string fileFullPath)
        {
            string normalizedPath = MediaPathNormalizer.Normalize(fileFullPath);
            if (string.IsNullOrWhiteSpace(dbFullPath)
                || string.IsNullOrWhiteSpace(normalizedPath)
                || !TryCreateMovieInfo(normalizedPath, out MovieInfo movieInfo))
            {
                return null;
            }

            if (FolderCheckService.IsFileRegisteredInDb(dbFullPath, normalizedPath))
            {
                return null;
            }

            if (TryReviveStaleMovieRecord(dbFullPath, movieInfo)
                && FolderCheckService.IsFileRegisteredInDb(dbFullPath, normalizedPath))
            {
                return movieInfo;
            }

            bool inserted = await InsertMovieTable(dbFullPath, movieInfo).ConfigureAwait(false);
            if (!inserted && !TryReviveStaleMovieRecord(dbFullPath, movieInfo))
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] insert/revive failed: {normalizedPath}");
                return null;
            }

            if (!FolderCheckService.IsFileRegisteredInDb(dbFullPath, normalizedPath))
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] not visible in db after insert: {normalizedPath}");
                return null;
            }

            return movieInfo;
        }

        /// <summary>
        /// パスは消えたが movie_name / hash が残っている等、既存行を更新して復活させる。
        /// </summary>
        private static bool TryReviveStaleMovieRecord(string dbFullPath, MovieInfo movieInfo)
        {
            if (movieInfo == null || string.IsNullOrWhiteSpace(dbFullPath))
            {
                return false;
            }

            try
            {
                long? movieId;
                using (SQLiteConnection lookup = new($"Data Source={dbFullPath}"))
                {
                    lookup.Open();
                    movieId = FindRevivableMovieId(lookup, movieInfo);
                }

                if (movieId == null)
                {
                    return false;
                }

                // sinku は接続外で取得（復活時も詳細パネル用メタを埋める）
                ResolveInsertMediaFields(
                    movieInfo,
                    out string container,
                    out string video,
                    out string audio,
                    out string extra,
                    out long movieLengthLong);

                using SQLiteConnection update = new($"Data Source={dbFullPath}");
                update.Open();
                using SQLiteCommand cmd = update.CreateCommand();
                cmd.CommandText =
                    "UPDATE movie SET " +
                    "movie_path = @movie_path, " +
                    "movie_size = @movie_size, " +
                    "file_date = @file_date, " +
                    "hash = @hash, " +
                    "movie_length = @movie_length, " +
                    "container = @container, " +
                    "video = @video, " +
                    "audio = @audio, " +
                    "extra = @extra " +
                    "WHERE movie_id = @movie_id";
                cmd.Parameters.AddWithValue("@movie_path", movieInfo.MoviePath);
                cmd.Parameters.AddWithValue("@movie_size", movieInfo.MovieSize / 1024);
                cmd.Parameters.AddWithValue("@file_date", movieInfo.FileDate.ToLocalTime());
                cmd.Parameters.AddWithValue("@hash", movieInfo.Hash ?? "");
                cmd.Parameters.AddWithValue("@movie_length", movieLengthLong);
                cmd.Parameters.AddWithValue("@container", container ?? "");
                cmd.Parameters.AddWithValue("@video", video ?? "");
                cmd.Parameters.AddWithValue("@audio", audio ?? "");
                cmd.Parameters.AddWithValue("@extra", extra ?? "");
                cmd.Parameters.AddWithValue("@movie_id", movieId.Value);
                if (cmd.ExecuteNonQuery() <= 0)
                {
                    return false;
                }

                movieInfo.MovieId = movieId.Value;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] revive failed: {movieInfo.MoviePath} : {ex.Message}");
                return false;
            }
        }

        private static long? FindRevivableMovieId(SQLiteConnection connection, MovieInfo movieInfo)
        {
            string movieName = movieInfo.MovieName?.ToLowerInvariant() ?? "";
            string hash = movieInfo.Hash ?? "";

            if (!string.IsNullOrEmpty(hash))
            {
                long? byHash = FindRevivableMovieId(
                    connection,
                    "SELECT movie_id, movie_path FROM movie WHERE hash = @hash",
                    [("@hash", hash)]);
                if (byHash != null)
                {
                    movieInfo.MovieId = byHash.Value;
                    return byHash;
                }
            }

            if (!string.IsNullOrEmpty(movieName))
            {
                long? byName = FindRevivableMovieId(
                    connection,
                    "SELECT movie_id, movie_path FROM movie WHERE lower(movie_name) = lower(@movie_name)",
                    [("@movie_name", movieName)]);
                if (byName != null)
                {
                    movieInfo.MovieId = byName.Value;
                    return byName;
                }
            }

            return null;
        }

        private static long? FindRevivableMovieId(
            SQLiteConnection connection,
            string sql,
            IReadOnlyList<(string Name, object Value)> parameters)
        {
            using SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach ((string name, object value) in parameters)
            {
                cmd.Parameters.AddWithValue(name, value);
            }

            using SQLiteDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long movieId = reader.GetInt64(0);
                string storedPath = reader.IsDBNull(1) ? "" : reader.GetString(1);
                if (string.IsNullOrWhiteSpace(storedPath) || !File.Exists(storedPath))
                {
                    return movieId;
                }
            }

            return null;
        }
    }
}
