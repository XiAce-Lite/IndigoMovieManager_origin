using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class TagBarServiceTests
{
    [Fact]
    public void LoadInto_orders_by_order_id_then_item_id()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("item_id", typeof(long));
        table.Columns.Add("parent_id", typeof(long));
        table.Columns.Add("order_id", typeof(long));
        table.Columns.Add("group_id", typeof(long));
        table.Columns.Add("title", typeof(string));
        table.Columns.Add("contents", typeof(string));

        table.Rows.Add(2L, 0L, 1L, 0L, "B", "b");
        table.Rows.Add(1L, 0L, 0L, 0L, "A", "a");
        table.Rows.Add(3L, 0L, 1L, 0L, "C", "c");

        var target = new System.Collections.ObjectModel.ObservableCollection<TagBarItem>();
        TagBarService.LoadInto(table, target);

        Assert.Equal(3, target.Count);
        Assert.Equal("A", target[0].Title);
        Assert.Equal("B", target[1].Title);
        Assert.Equal("C", target[2].Title);
    }

    [Fact]
    public void BuildDuplicateTitle_appends_copy_suffix()
    {
        Assert.Equal("未視聴 (コピー)", TagBarService.BuildDuplicateTitle("未視聴"));
        Assert.Equal("無題 (コピー)", TagBarService.BuildDuplicateTitle(""));
    }

    [Fact]
    public void GetEffectiveContents_uses_title_when_contents_empty()
    {
        var item = new TagBarItem
        {
            Title = "★★★",
            Contents = "",
        };

        Assert.Equal("★★★", TagBarService.GetEffectiveContents(item));
        Assert.Equal("★★★", item.EffectiveContents);
    }

    [Fact]
    public void GetEffectiveContents_prefers_contents_when_present()
    {
        var item = new TagBarItem
        {
            Title = "表示名",
            Contents = "{tag = ''}",
        };

        Assert.Equal("{tag = ''}", TagBarService.GetEffectiveContents(item));
    }

    [Fact]
    public void TryNormalizeSaveFields_copies_title_when_contents_empty()
    {
        string title = "★★★";
        string contents = "";
        Assert.True(TagBarService.TryNormalizeSaveFields(ref title, ref contents));
        Assert.Equal("★★★", title);
        Assert.Equal("★★★", contents);
    }

    [Fact]
    public void TryNormalizeSaveFields_copies_contents_when_title_empty()
    {
        string title = "";
        string contents = "{tag = ''}";
        Assert.True(TagBarService.TryNormalizeSaveFields(ref title, ref contents));
        Assert.Equal("{tag = ''}", title);
        Assert.Equal("{tag = ''}", contents);
    }

    [Fact]
    public void TryNormalizeSaveFields_fails_when_both_empty()
    {
        string title = "";
        string contents = "   ";
        Assert.False(TagBarService.TryNormalizeSaveFields(ref title, ref contents));
    }

    [Fact]
    public void ExpandContentsForTagAppend_splits_on_whitespace()
    {
        string expanded = TagBarService.ExpandContentsForTagAppend("foo bar  baz");
        Assert.Equal($"foo{Environment.NewLine}bar{Environment.NewLine}baz", expanded);
    }

    [Theory]
    [InlineData("★")]
    [InlineData("★★")]
    [InlineData("★★★")]
    [InlineData("★★★★")]
    [InlineData("★★★★★")]
    public void IsBuiltInStarRating_recognizes_default_star_buttons(string title)
    {
        var item = new TagBarItem { Title = title };
        Assert.True(TagBarService.IsBuiltInStarRating(item));
    }

    [Theory]
    [InlineData("★★★ (コピー)")]
    [InlineData("未視聴")]
    [InlineData("")]
    public void IsBuiltInStarRating_rejects_other_titles(string title)
    {
        var item = new TagBarItem { Title = title };
        Assert.False(TagBarService.IsBuiltInStarRating(item));
    }

    [Fact]
    public void CreateDatabase_includes_all_built_in_star_ratings()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-tagbar-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            var titles = ReadTagBarTitles(dbPath);

            Assert.Equal(TagBarService.BuiltInStarRatingTitles.Length, titles.Count);
            foreach (string title in TagBarService.BuiltInStarRatingTitles)
            {
                Assert.Contains(title, titles);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void EnsureBuiltInStarRatingItems_inserts_missing_items()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-tagbar-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);
            ClearTagBarTable(dbPath);
            InsertTagBarRow(dbPath, 1, 0, "★★★");

            int inserted = SQLite.EnsureBuiltInStarRatingItems(dbPath);
            var titles = ReadTagBarTitles(dbPath);

            Assert.Equal(4, inserted);
            Assert.Equal(TagBarService.BuiltInStarRatingTitles.Length, titles.Count);
            foreach (string title in TagBarService.BuiltInStarRatingTitles)
            {
                Assert.Contains(title, titles);
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void EnsureBuiltInStarRatingItems_is_idempotent_when_all_present()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"imm-tagbar-{Guid.NewGuid():N}.wb");
        try
        {
            SQLite.CreateDatabase(dbPath);

            Assert.Equal(0, SQLite.EnsureBuiltInStarRatingItems(dbPath));
            Assert.Equal(TagBarService.BuiltInStarRatingTitles.Length, ReadTagBarTitles(dbPath).Count);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static HashSet<string> ReadTagBarTitles(string dbPath)
    {
        using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "select title from tagbar";
        var titles = new HashSet<string>(StringComparer.Ordinal);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            titles.Add(reader[0].ToString().Trim());
        }

        return titles;
    }

    private static void ClearTagBarTable(string dbPath)
    {
        using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "delete from tagbar";
        cmd.ExecuteNonQuery();
    }

    private static void InsertTagBarRow(string dbPath, long itemId, long orderId, string title)
    {
        using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "insert into tagbar (item_id, parent_id, order_id, group_id, title, contents) " +
            "values (@item_id, 0, @order_id, 0, @title, '')";
        cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@item_id", itemId));
        cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@order_id", orderId));
        cmd.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@title", title));
        cmd.ExecuteNonQuery();
    }
}
