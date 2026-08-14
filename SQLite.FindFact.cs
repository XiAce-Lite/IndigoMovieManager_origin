using System.Data.SQLite;
using IndigoMovieManager.Data;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        public static void InsertFindFactTable(string dbFullPath, string find_text)
        {
            if (string.IsNullOrEmpty(dbFullPath) || string.IsNullOrEmpty(find_text))
            {
                return;
            }

            var now = DateTime.Now;
            var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                // 存在判定とINSERT/UPDATEを1文のUPSERTにまとめる。
                // SELECTを文字列補間していたためキーワードにシングルクオートが含まれると
                // 存在判定が壊れ、UNIQUE制約違反(find_text)になっていた。
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText =
                    "insert into findfact (find_text, find_count, last_date) " +
                    "values (@find_text, 1, @last_date) " +
                    "on conflict(find_text) do update set " +
                    "find_count = find_count + 1, last_date = @last_date";
                cmd.Parameters.Add(new SQLiteParameter("@find_text", find_text));
                cmd.Parameters.Add(new SQLiteParameter("@last_date", result));
                cmd.ExecuteNonQuery();
            });
        }
    }
}
