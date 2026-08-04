namespace IndigoMovieManager.Services;

/// <summary>
/// 詳細ペインのクリックから { } SQL 検索式を組み立てる（手入力 {} は対象外）。
/// </summary>
internal static class BraceFieldSearchBuilder
{
    /// <summary>Artist 行全文の等価検索。</summary>
    public static string BuildArtistEquals(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return "";
        }

        return "{artist = " + QuoteLiteral(word.Trim()) + "}";
    }

    /// <summary>Comment3 の語単位 LIKE 検索（ESCAPE '\'）。</summary>
    public static string BuildComment3Like(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return "";
        }

        string pattern = "%" + EscapeLikeMetacharacters(word.Trim()) + "%";
        return "{comment3 like " + QuoteLiteral(pattern) + @" ESCAPE '\'}";
    }

    internal static string QuoteLiteral(string value) =>
        "'" + (value ?? "").Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>LIKE のメタ文字を ESCAPE '\' 前提でエスケープ。</summary>
    internal static string EscapeLikeMetacharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}
