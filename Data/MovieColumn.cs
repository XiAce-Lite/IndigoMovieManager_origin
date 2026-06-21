namespace IndigoMovieManager.Data
{
  /// <summary>
  /// movie テーブルの更新可能カラム。動的 SQL インジェクションを防ぐ。
  /// </summary>
  public enum MovieColumn
  {
    Tag,
    Score,
    View_Count,
    Movie_Name,
    Movie_Path,
    Comment1,
    Comment2,
    Comment3,
  }

  internal static class MovieColumnExtensions
  {
    public static string ToColumnName(this MovieColumn column)
    {
      return column switch
      {
        MovieColumn.Tag => "tag",
        MovieColumn.Score => "score",
        MovieColumn.View_Count => "view_count",
        MovieColumn.Movie_Name => "movie_name",
        MovieColumn.Movie_Path => "movie_path",
        MovieColumn.Comment1 => "comment1",
        MovieColumn.Comment2 => "comment2",
        MovieColumn.Comment3 => "comment3",
        _ => throw new ArgumentOutOfRangeException(nameof(column)),
      };
    }

    public static bool TryParseColumnName(string columnName, out MovieColumn column)
    {
      foreach (MovieColumn value in Enum.GetValues<MovieColumn>())
      {
        if (string.Equals(value.ToColumnName(), columnName, StringComparison.OrdinalIgnoreCase))
        {
          column = value;
          return true;
        }
      }

      column = default;
      return false;
    }
  }
}
