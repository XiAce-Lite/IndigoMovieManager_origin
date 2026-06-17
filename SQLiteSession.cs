using System.Data;
using System.Data.SQLite;

namespace IndigoMovieManager
{
    /// <summary>
    /// 1接続を使い回して複数クエリを実行する。
    /// </summary>
    internal sealed class SQLiteSession : IDisposable
    {
        private readonly SQLiteConnection _connection;

        public SQLiteSession(string dbFullPath)
        {
            _connection = new SQLiteConnection($"Data Source={dbFullPath}");
            _connection.Open();
        }

        public DataTable Query(string sql)
        {
            DataTable dt = new();
            using SQLiteCommand cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            using SQLiteDataAdapter da = new(cmd);
            da.Fill(dt);
            return dt;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
