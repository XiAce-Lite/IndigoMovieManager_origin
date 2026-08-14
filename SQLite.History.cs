using System.Data.SQLite;
using System.Reflection;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        private static readonly object HistoryInsertLock = new();

        /// <summary>
        /// 同一 find_text の履歴行をすべて削除する。
        /// UI 側が Find_Id=0（INSERT 未フィードバック）でも消せるように text 基準。
        /// </summary>
        public static void DeleteHistoryTable(string dbFullPath, string findText)
        {
            if (string.IsNullOrEmpty(dbFullPath) || string.IsNullOrEmpty(findText))
            {
                return;
            }

            lock (HistoryInsertLock)
            {
                SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
                {
                    using SQLiteCommand cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "delete from history where find_text = @find_text";
                    cmd.Parameters.Add(new SQLiteParameter("@find_text", findText));
                    cmd.ExecuteNonQuery();
                });
            }
        }

        public static void InsertHistoryTable(string dbFullPath, string find_text)
        {
            if (string.IsNullOrEmpty(dbFullPath) || string.IsNullOrEmpty(find_text))
            {
                return;
            }

            lock (HistoryInsertLock)
            {
                try
                {
                    using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                    connection.Open();

                    var now = DateTime.Now;
                    var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                    using var transaction = connection.BeginTransaction();

                    using (SQLiteCommand updateCmd = connection.CreateCommand())
                    {
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText =
                            "update history set find_date = @find_date where find_text = @find_text";
                        updateCmd.Parameters.Add(new SQLiteParameter("@find_date", result));
                        updateCmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                        if (updateCmd.ExecuteNonQuery() > 0)
                        {
                            transaction.Commit();
                            return;
                        }
                    }

                    long find_id = 1;
                    using (SQLiteCommand selectCmd = connection.CreateCommand())
                    {
                        selectCmd.Transaction = transaction;
                        selectCmd.CommandText = "select max(find_id) from history";
                        object maxId = selectCmd.ExecuteScalar();
                        if (maxId != null && maxId != DBNull.Value)
                        {
                            find_id = Convert.ToInt64(maxId) + 1;
                        }
                    }

                    using (SQLiteCommand insertCmd = connection.CreateCommand())
                    {
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText =
                            "insert into history (find_id,find_text,find_date) values (@find_id,@find_text,@find_date)";
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_id", find_id));
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                        insertCmd.Parameters.Add(new SQLiteParameter("@find_date", result));
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                    UiErrorReporter.ShowError(e.Message, title);
                }
            }
        }

        public static void DeleteHistoryTable(string dbFullPath, int keepHistoryCount)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    $"DELETE from history where find_id < " +
                    $"(select find_id from " +
                    $"  (select find_id from history order by find_id desc LIMIT {keepHistoryCount}) " +
                    $" order by find_id limit 1)";
                cmd.ExecuteNonQuery();
            });
        }
    }
}
