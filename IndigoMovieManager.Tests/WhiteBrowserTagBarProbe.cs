using System.Data.SQLite;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace IndigoMovieManager.Tests;

public class WhiteBrowserTagBarProbe
{
    private readonly ITestOutputHelper _output;

    public WhiteBrowserTagBarProbe(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Dump_tagbar_from_whitebrowser_db()
    {
        string path = @"F:\WhiteBrowser\Xドライブ用.wb";
        if (!File.Exists(path))
        {
            _output.WriteLine("WB file not found, skipped.");
            return;
        }

        using var c = new SQLiteConnection($"Data Source={path}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT item_id, parent_id, order_id, group_id, title, contents FROM tagbar ORDER BY order_id, item_id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _output.WriteLine(
                $"{r[0]}\t{r[1]}\t{r[2]}\t{r[3]}\t\"{r[4]}\"\t\"{r[5]}\"");
        }
    }

    [Theory]
    [InlineData("movie_size < 50000")]
    [InlineData("tag = ''")]
    [InlineData("tag <> ''")]
    [InlineData("container = 'zip'")]
    public void Probe_where_clause(string whereClause)
    {
        string path = @"F:\WhiteBrowser\Xドライブ用.wb";
        if (!File.Exists(path))
        {
            _output.WriteLine("WB file not found, skipped.");
            return;
        }

        try
        {
            using var c = new SQLiteConnection($"Data Source={path}");
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT movie_id FROM movie WHERE ({whereClause})";
            int count = 0;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                count++;
            }

            _output.WriteLine($"WHERE ({whereClause}) -> {count} rows");
        }
        catch (Exception e)
        {
            _output.WriteLine($"WHERE ({whereClause}) -> EXCEPTION: {e.GetType().Name}: {e.Message}");
            throw;
        }
    }
}
