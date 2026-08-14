using System.Data;
using System.Data.SQLite;
using IndigoMovieManager.Data;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        public static void UpsertSystemTable(string dbFullPath, string attr, string value)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return;
            }

            DataTable dt = GetData(dbFullPath, $"select * from system where attr = '{attr}'");
            if (dt == null)
            {
                return;
            }

            if (dt.Rows.Count > 0)
            {
                UpdateSystemTable(dbFullPath, attr, value);
            }
            else
            {
                InsertSystemTable(dbFullPath, attr, value);
            }
        }

        private static void InsertSystemTable(string dbFullPath, string attr, string value)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "insert into system (attr, value) values (@attr, @value)";
                cmd.Parameters.Add(new SQLiteParameter("@attr", attr));
                cmd.Parameters.Add(new SQLiteParameter("@value", value));
                cmd.ExecuteNonQuery();
            });
        }

        private static void UpdateSystemTable(string dbFullPath, string attr, string value)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "update system set value = @value where attr = @attr";
                cmd.Parameters.Add(new SQLiteParameter("@attr", attr));
                cmd.Parameters.Add(new SQLiteParameter("@value", value));
                cmd.ExecuteNonQuery();
            });
        }
    }
}
