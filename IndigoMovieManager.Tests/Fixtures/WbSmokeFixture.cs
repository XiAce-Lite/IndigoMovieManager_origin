using System.Data;
using Xunit;

namespace IndigoMovieManager.Tests.Fixtures;

internal sealed class WbSmokeFixture : IDisposable
{
    private readonly string _rootDir;

    public WbSmokeFixture()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), $"imm-wb-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDir);
        DbPath = Path.Combine(_rootDir, "sample.wb");
    }

    public string DbPath { get; }

    public void CreatePopulatedDatabase()
    {
        SQLite.CreateDatabase(DbPath);
        SQLite.UpsertSystemTable(DbPath, "skin", "DefaultSmall");
        SQLite.UpsertSystemTable(DbPath, "thum", @"C:\thumb\abcd-123");
        SQLite.InsertTagBarItem(DbPath, "サンプル検索", "{tag = 'sample'}");
        SQLite.InsertHistoryTable(DbPath, "abcd-123");
    }

    public DataTable Query(string sql)
    {
        DataTable table = SQLite.GetData(DbPath, sql);
        Assert.NotNull(table);
        return table;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDir))
            {
                Directory.Delete(_rootDir, recursive: true);
            }
        }
        catch
        {
        }
    }
}
