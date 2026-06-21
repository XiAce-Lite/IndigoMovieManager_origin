using System.Data;

namespace IndigoMovieManager.Services
{
    internal static class DatabaseQueryHelper
    {
        public static DataTable Query(string dbPath, string sql, SQLiteSession session = null)
        {
            return session?.Query(sql) ?? SQLite.GetData(dbPath, sql);
        }
    }
}
