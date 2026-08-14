using System.Data;
using IndigoMovieManager.Tests.Fixtures;
using Xunit;

namespace IndigoMovieManager.Tests;

public class WbSmokeTests
{
    [Fact]
    public void CreateDatabase_creates_expected_tables()
    {
        using var fixture = new WbSmokeFixture();

        SQLite.CreateDatabase(fixture.DbPath);

        Assert.Single(fixture.Query("select name from sqlite_master where type = 'table' and name = 'movie'").Rows);
        Assert.Single(fixture.Query("select name from sqlite_master where type = 'table' and name = 'tagbar'").Rows);
        Assert.Single(fixture.Query("select name from sqlite_master where type = 'table' and name = 'history'").Rows);
        Assert.Single(fixture.Query("select name from sqlite_master where type = 'table' and name = 'system'").Rows);
    }

    [Fact]
    public void GoldenFixture_supports_system_tagbar_and_history_roundtrip()
    {
        using var fixture = new WbSmokeFixture();
        fixture.CreatePopulatedDatabase();

        Assert.Equal("DefaultSmall", fixture.Query("select value from system where attr = 'skin'").Rows[0][0]);
        Assert.Equal(@"C:\thumb\abcd-123", fixture.Query("select value from system where attr = 'thum'").Rows[0][0]);
        Assert.Contains(
            fixture.Query("select title from tagbar").AsEnumerable().Select(row => row[0].ToString()),
            value => value == "サンプル検索");
        Assert.Contains(
            fixture.Query("select find_text from history").AsEnumerable().Select(row => row[0].ToString()),
            value => value == "abcd-123");
    }

    [Fact]
    public void DeleteHistoryTable_by_find_text_removes_rows_even_when_ui_would_have_zero_id()
    {
        using var fixture = new WbSmokeFixture();
        SQLite.CreateDatabase(fixture.DbPath);

        SQLite.InsertHistoryTable(fixture.DbPath, "abcd-123");
        Assert.Equal(1, fixture.Query("select count(*) from history where find_text = 'abcd-123'").Rows[0].Field<long>(0));

        // UI が Find_Id=0 のままでも使う削除経路（text 基準）
        SQLite.DeleteHistoryTable(fixture.DbPath, "abcd-123");

        Assert.Equal(0, fixture.Query("select count(*) from history where find_text = 'abcd-123'").Rows[0].Field<long>(0));
    }
}
