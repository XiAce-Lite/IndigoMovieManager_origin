using System.Data;
using System.Data.SQLite;
using System.Reflection;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        public static void InsertBookmarkTable(
            string dbFullPath,
            MovieInfo mvi,
            string sourceMoviePath,
            string sourceHash = "")
        {
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                // データベースから最大IDを取得
                string sql = "select max(movie_id) from bookmark";
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
                        mvi.MovieId = 1;    //ゼロ行なので、1
                    }
                }

                var now = DateTime.Now;
                var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "insert into bookmark (" +
                        "   movie_id," +
                        "   movie_name," +
                        "   movie_path," +
                        "   last_date," +
                        "   file_date," +
                        "   regist_date," +
                        "   hash," +
                        "   comment1)" +
                        "   values (" +
                        "   @movie_id," +
                        "   @movie_name," +
                        "   @movie_path," +
                        "   @last_date," +
                        "   @file_date," +
                        "   @regist_date," +
                        "   @hash," +
                        "   @comment1)";

                    cmd.Parameters.Add(new SQLiteParameter("@movie_id", mvi.MovieId));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_name", mvi.MovieName.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@movie_path", mvi.MoviePath.ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@last_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@file_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@regist_date", result));
                    cmd.Parameters.Add(new SQLiteParameter("@hash", (sourceHash ?? "").ToLower()));
                    cmd.Parameters.Add(new SQLiteParameter("@comment1", (sourceMoviePath ?? "").ToLower()));
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
            }

            // 例外が発生した場合
            catch (Exception e)
            {
                // 例外の内容を表示します。
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        public static void UpdateBookmarkViewCount(string dbFullPath, long movieId)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "update bookmark set view_count = view_count + 1 where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.ExecuteNonQuery();
            });
        }

        public static void UpdateBookmarkSource(string dbFullPath, long movieId, string sourceMoviePath, string sourceHash)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    "update bookmark set comment1 = @comment1, hash = @hash where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movieId));
                cmd.Parameters.Add(new SQLiteParameter("@comment1", (sourceMoviePath ?? "").ToLower()));
                cmd.Parameters.Add(new SQLiteParameter("@hash", (sourceHash ?? "").ToLower()));
                cmd.ExecuteNonQuery();
            });
        }

        public static void UpdateBookmarkRename(string dbFullPath, string oldName, string newName)
        {
            oldName = oldName.ToLower();
            newName = newName.ToLower();

            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    $"update bookmark set " +
                    $"movie_name = replace(movie_name,'{oldName}', '{newName}'), " +
                    $"movie_path = replace(movie_path,'{oldName}', '{newName}') " +
                    $"where lower(movie_name) like '%{oldName}%'";
                cmd.ExecuteNonQuery();
            });
        }

        public static void DeleteBookmarkTable(string dbFullPath, long movie_id)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE from bookmark where movie_id = @id";
                cmd.Parameters.Add(new SQLiteParameter("@id", movie_id));
                cmd.ExecuteNonQuery();
            });
        }
    }
}
