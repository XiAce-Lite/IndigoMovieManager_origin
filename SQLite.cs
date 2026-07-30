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
        public static DataTable GetData(string dbFullPath, string sql)
        {
            return SqliteDataAccess.Query(dbFullPath, sql);
        }
    }
}
