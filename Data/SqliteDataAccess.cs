using System.Data;
using System.Data.SQLite;
using System.Reflection;
using System.Runtime.CompilerServices;
using IndigoMovieManager.Services;

namespace IndigoMovieManager.Data
{
  /// <summary>
  /// SQLite 接続・クエリ実行。UI へのエラー表示は IDataErrorReporter に委譲する。
  /// </summary>
  internal static class SqliteDataAccess
  {
    private static readonly IDataErrorReporter ErrorReporter = new MessageBoxErrorReporter();

    public static DataTable Query(string dbFullPath, string sql, [CallerMemberName] string caller = "")
    {
      try
      {
        DataTable dt = new();
        using SQLiteConnection connection = new($"Data Source={dbFullPath}");
        connection.Open();
        using SQLiteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using SQLiteDataAdapter da = new(cmd);
        da.Fill(dt);
        return dt;
      }
      catch (Exception e)
      {
        ReportError(e, caller);
        return null;
      }
    }

    public static void ExecuteNonQuery(
      string dbFullPath,
      Action<SQLiteConnection, SQLiteTransaction> action,
      [CallerMemberName] string caller = "")
    {
      try
      {
        using SQLiteConnection connection = new($"Data Source={dbFullPath}");
        connection.Open();
        using SQLiteTransaction transaction = connection.BeginTransaction();
        action(connection, transaction);
        transaction.Commit();
      }
      catch (Exception e)
      {
        ReportError(e, caller);
      }
    }

    internal static void ReportError(Exception e, [CallerMemberName] string caller = "")
    {
      string title = $"{Assembly.GetExecutingAssembly().GetName().Name} - {caller}";
      AppFileLogger.LogError(e, caller, e.Message);
      ErrorReporter.Report(e.Message, title);
    }
  }
}
