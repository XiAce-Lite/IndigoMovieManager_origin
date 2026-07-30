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
        public static void EnsureTagBarTable(string dbFullPath)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return;
            }

            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS tagbar(
                        item_id integer primary key not null,
                        parent_id integer not null default 0,
                        order_id integer not null default 0,
                        group_id integer not null default 0,
                        title text not null default '',
                        contents text not null default '' )";
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                var title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, title);
            }
        }

        /// <summary>
        /// 既定の★評価（★〜★★★★★）が tagbar に無ければ挿入する。挿入件数を返す。
        /// </summary>

        public static int EnsureBuiltInStarRatingItems(string dbFullPath)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return 0;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                int inserted = EnsureBuiltInStarRatingItemsCore(connection, transaction);
                transaction.Commit();
                return inserted;
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
                return 0;
            }
        }

        private static int EnsureBuiltInStarRatingItemsCore(
            SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            HashSet<string> existingTitles = new(StringComparer.Ordinal);
            using (SQLiteCommand selectTitlesCmd = connection.CreateCommand())
            {
                selectTitlesCmd.Transaction = transaction;
                selectTitlesCmd.CommandText = "select title from tagbar";
                using SQLiteDataReader reader = selectTitlesCmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                    {
                        existingTitles.Add(reader[0].ToString().Trim());
                    }
                }
            }

            long nextItemId = 1;
            using (SQLiteCommand selectMaxIdCmd = connection.CreateCommand())
            {
                selectMaxIdCmd.Transaction = transaction;
                selectMaxIdCmd.CommandText = "select max(item_id) from tagbar";
                object maxId = selectMaxIdCmd.ExecuteScalar();
                if (maxId != DBNull.Value)
                {
                    nextItemId = Convert.ToInt64(maxId) + 1;
                }
            }

            int inserted = 0;
            for (int i = 0; i < TagBarService.BuiltInStarRatingTitles.Length; i++)
            {
                string title = TagBarService.BuiltInStarRatingTitles[i];
                if (existingTitles.Contains(title))
                {
                    continue;
                }

                using SQLiteCommand insertCmd = connection.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText =
                    "insert into tagbar (item_id, parent_id, order_id, group_id, title, contents) " +
                    "values (@item_id, 0, @order_id, 0, @title, '')";
                insertCmd.Parameters.Add(new SQLiteParameter("@item_id", nextItemId));
                insertCmd.Parameters.Add(new SQLiteParameter("@order_id", i));
                insertCmd.Parameters.Add(new SQLiteParameter("@title", title));
                insertCmd.ExecuteNonQuery();

                nextItemId++;
                inserted++;
            }

            return inserted;
        }

        public static long InsertTagBarItem(string dbFullPath, string title, string contents)
        {
            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();

                long itemId = 1;
                long orderId = 0;
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.CommandText = "select max(item_id), max(order_id) from tagbar";
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        if (reader[0] != DBNull.Value)
                        {
                            itemId = Convert.ToInt64(reader[0]) + 1;
                        }

                        if (reader[1] != DBNull.Value)
                        {
                            orderId = Convert.ToInt64(reader[1]) + 1;
                        }
                    }
                }

                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "insert into tagbar (item_id, parent_id, order_id, group_id, title, contents) " +
                        "values (@item_id, 0, @order_id, 0, @title, @contents)";
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.Parameters.Add(new SQLiteParameter("@order_id", orderId));
                    cmd.Parameters.Add(new SQLiteParameter("@title", title ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@contents", contents ?? ""));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
                return itemId;
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
                return 0;
            }
        }

        public static void UpdateTagBarItem(string dbFullPath, long itemId, string title, string contents)
        {
            if (itemId <= 0)
            {
                return;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.Transaction = transaction;
                    selectCmd.CommandText =
                        "select order_id, title, contents from tagbar where item_id = @item_id";
                    selectCmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read()
                        && TagBarService.IsBuiltInStarRatingRow(
                            Convert.ToInt64(reader["order_id"]),
                            reader["title"]?.ToString(),
                            reader["contents"]?.ToString()))
                    {
                        return;
                    }
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "update tagbar set title = @title, contents = @contents where item_id = @item_id";
                    cmd.Parameters.Add(new SQLiteParameter("@title", title ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@contents", contents ?? ""));
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
            }
        }

        public static void DeleteTagBarItem(string dbFullPath, long itemId)
        {
            if (itemId <= 0)
            {
                return;
            }

            EnsureTagBarTable(dbFullPath);
            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using var transaction = connection.BeginTransaction();
                using (SQLiteCommand selectCmd = connection.CreateCommand())
                {
                    selectCmd.Transaction = transaction;
                    selectCmd.CommandText =
                        "select order_id, title, contents from tagbar where item_id = @item_id";
                    selectCmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    using SQLiteDataReader reader = selectCmd.ExecuteReader();
                    if (reader.Read()
                        && TagBarService.IsBuiltInStarRatingRow(
                            Convert.ToInt64(reader["order_id"]),
                            reader["title"]?.ToString(),
                            reader["contents"]?.ToString()))
                    {
                        return;
                    }
                }

                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "delete from tagbar where item_id = @item_id";
                    cmd.Parameters.Add(new SQLiteParameter("@item_id", itemId));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                var dialogTitle = $"{Assembly.GetExecutingAssembly().GetName().Name} - {MethodBase.GetCurrentMethod().Name}";
                UiErrorReporter.ShowError(e.Message, dialogTitle);
            }
        }
    }
}
