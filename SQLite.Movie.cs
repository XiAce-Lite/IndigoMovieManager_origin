using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        public static void UpdateMovieSingleColumn(string dbFullPath, long movieId, string columnName, object value)
        {
            if (!MovieColumnExtensions.TryParseColumnName(columnName, out MovieColumn column))
            {
                UpdateMovieSingleColumnByName(dbFullPath, movieId, columnName, value);
                return;
            }

            UpdateMovieSingleColumn(dbFullPath, movieId, column, value);
        }

        public static void UpdateMovieSingleColumn(string dbFullPath, long movieId, MovieColumn column, object value) =>
            UpdateMovieSingleColumnByName(dbFullPath, movieId, column.ToColumnName(), value);

        private static void UpdateMovieSingleColumnByName(string dbFullPath, long movieId, string columnName, object value)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = $"update movie set {columnName} = @value where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.Parameters.Add(new SQLiteParameter("@value", value));
                cmd.ExecuteNonQuery();
            });
        }

        public static void UpdateMovieFileInfo(
            string dbFullPath,
            long movieId,
            SinkuMetadata metadata,
            long existingMovieLengthSec)
        {
            if (metadata == null)
            {
                return;
            }

            long movieLengthSec = existingMovieLengthSec;
            if (existingMovieLengthSec < 1 && metadata.MovieLengthSec > 0)
            {
                movieLengthSec = metadata.MovieLengthSec;
            }

            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                if (existingMovieLengthSec < 1 && metadata.MovieLengthSec > 0)
                {
                    cmd.CommandText =
                        "update movie set container = @container, video = @video, audio = @audio, " +
                        "extra = @extra, movie_length = @movie_length where movie_id = @id";
                    cmd.Parameters.Add(new SQLiteParameter("@movie_length", movieLengthSec));
                }
                else
                {
                    cmd.CommandText =
                        "update movie set container = @container, video = @video, audio = @audio, " +
                        "extra = @extra where movie_id = @id";
                }

                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.Parameters.Add(new SQLiteParameter("@container", metadata.Container ?? ""));
                cmd.Parameters.Add(new SQLiteParameter("@video", metadata.Video ?? ""));
                cmd.Parameters.Add(new SQLiteParameter("@audio", metadata.Audio ?? ""));
                cmd.Parameters.Add(new SQLiteParameter("@extra", metadata.Extra ?? ""));
                cmd.ExecuteNonQuery();
            });
        }

        public static void UpdateMovieZipInfo(string dbFullPath, long movieId, int imageCount)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    "update movie set container = @container, video = '', audio = '', extra = '', " +
                    "movie_length = @movie_length where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.Parameters.Add(new SQLiteParameter("@container", "zip"));
                cmd.Parameters.Add(new SQLiteParameter("@movie_length", imageCount));
                cmd.ExecuteNonQuery();
            });
        }

        public static void DeleteMovieTable(string dbFullPath, long movieId)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "delete from movie where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.ExecuteNonQuery();
            });
        }

        public static async Task<bool> InsertMovieTable(string dbFullPath, MovieInfo mvi)
        {
            try
            {
                mvi.MoviePath = MediaPathNormalizer.Normalize(mvi.MoviePath);

                // sinku は数秒かかることがある。SQLite 接続を開いたまま待たない。
                ResolveInsertMediaFields(
                    mvi,
                    out string container,
                    out string video,
                    out string audio,
                    out string extra,
                    out long movieLengthLong);

                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                // データベースから最大IDを取得
                string sql = "select max(movie_id) from movie";
                using SQLiteCommand selectCmd = connection.CreateCommand();
                selectCmd.CommandText = sql;

                // DataAdapterの生成
                SQLiteDataAdapter da = new(selectCmd);

                DataTable dt = new();
                da.Fill(dt);
                if (dt.Rows.Count < 1)
                {
                    mvi.MovieId = 1;    //ゼロ行なので、1
                }
                else
                {
                    if (dt.Rows[0][0].ToString() != "")
                    {
                        mvi.MovieId = (long)dt.Rows[0][0] + 1;  //Max + 1
                    }
                    else
                    {
                        //ここ、通らない気がする。
                        mvi.MovieId = 1;    //ゼロ行なので、1
                    }
                }

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "insert into movie (" +
                        "   movie_id," +
                        "   movie_name," +
                        "   movie_path," +
                        "   movie_length," +
                        "   movie_size," +
                        "   last_date," +
                        "   file_date," +
                        "   regist_date," +
                        "   hash, " +
                        "   container," +
                        "   video," +
                        "   audio," +
                        "   extra)" +
                        "   values (" +
                        "   @movie_id," +
                        "   @movie_name," +
                        "   @movie_path," +
                        "   @movie_length," +
                        "   @movie_size," +
                        "   @last_date," +
                        "   @file_date," +
                        "   @regist_date," +
                        "   @hash," +
                        "   @container," +
                        "   @video," +
                        "   @audio," +
                        "   @extra" +
                        ")";

                    cmd.Parameters.Add(new SQLiteParameter("@movie_id", mvi.MovieId));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_name", mvi.MovieName.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_path", mvi.MoviePath));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_length", movieLengthLong));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_size", mvi.MovieSize / 1024));
                    cmd.Parameters.Add(new SQLiteParameter("@last_date", mvi.LastDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@file_date", mvi.FileDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@regist_date", mvi.RegistDate.ToLocalTime()));
                    cmd.Parameters.Add(new SQLiteParameter("@hash", mvi.Hash));
                    cmd.Parameters.Add(new SQLiteParameter("@container", container));
                    cmd.Parameters.Add(new SQLiteParameter("@video", video));
                    cmd.Parameters.Add(new SQLiteParameter("@audio", audio));
                    cmd.Parameters.Add(new SQLiteParameter("@extra", extra));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
                return true;
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [SQLite] InsertMovieTable failed: {mvi?.MoviePath} : {e.Message}");
                return false;
            }
            finally
            {
                await Task.Delay(5).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 登録／復活時の container/video/audio/extra/length を解決する（DB 非接続）。
        /// </summary>
        internal static void ResolveInsertMediaFields(
            MovieInfo mvi,
            out string container,
            out string video,
            out string audio,
            out string extra,
            out long movieLengthLong)
        {
            container = "";
            video = "";
            audio = "";
            extra = "";
            movieLengthLong = mvi?.MovieLength ?? 0;

            if (mvi == null || string.IsNullOrWhiteSpace(mvi.MoviePath))
            {
                return;
            }

            if (!string.IsNullOrEmpty(mvi.Container)
                && string.Equals(mvi.Container, "zip", StringComparison.OrdinalIgnoreCase))
            {
                container = "zip";
                if (mvi.MovieLength > 0)
                {
                    movieLengthLong = mvi.MovieLength;
                }

                return;
            }

            if (SinkuMetadataFetcher.TryFetch(mvi.MoviePath, out SinkuMetadata metadata))
            {
                container = metadata.Container ?? "";
                video = metadata.Video ?? "";
                audio = metadata.Audio ?? "";
                extra = metadata.Extra ?? "";
                if (movieLengthLong < 1 && metadata.MovieLengthSec > 0)
                {
                    movieLengthLong = metadata.MovieLengthSec;
                }
            }

            if (!string.IsNullOrEmpty(mvi.Container))
            {
                container = mvi.Container;
            }
        }
    }
}
