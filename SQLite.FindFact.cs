using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using IndigoMovieManager.Data;
using IndigoMovieManager.Services;

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

            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                var now = DateTime.Now;
                var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));

                using var transaction = connection.BeginTransaction();

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
    }
}
