using System.Data.SQLite;
using IndigoMovieManager.Data;

namespace IndigoMovieManager
{
    internal partial class SQLite
    {
        public static void DeleteWatchTable(string dbFullPath)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "delete from watch";
                cmd.ExecuteNonQuery();
            });
        }

        public static void InsertWatchTable(string dbFullPath, WatchRecords watchRec)
        {
            SqliteDataAccess.ExecuteNonQuery(dbFullPath, (connection, transaction) =>
            {
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "insert into watch (dir,auto,watch,sub,dmm_auto) values (@dir,@auto,@watch,@sub,@dmm_auto)";
                cmd.Parameters.Add(new SQLiteParameter("@dir", watchRec.Dir));
                cmd.Parameters.Add(new SQLiteParameter("@auto", watchRec.Auto == true ? 1 : 0));
                cmd.Parameters.Add(new SQLiteParameter("@watch", watchRec.Watch == true ? 1 : 0));
                cmd.Parameters.Add(new SQLiteParameter("@sub", watchRec.Sub == true ? 1 : 0));
                cmd.Parameters.Add(new SQLiteParameter("@dmm_auto", watchRec.DmmAuto ? 1 : 0));
                cmd.ExecuteNonQuery();
            });
        }
    }
}
