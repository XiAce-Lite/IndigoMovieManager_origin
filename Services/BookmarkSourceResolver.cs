using System.IO;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
  /// <summary>
  /// ブックマークと元動画ファイルの対応付け。
  /// comment1 にフルパス、hash に元ファイルの CRC32 を保存する。
  /// </summary>
  internal static class BookmarkSourceResolver
  {
    public static MovieRecords FindMovieRecordByPath(IEnumerable<MovieRecords> records, string moviePath)
    {
      if (records == null || string.IsNullOrWhiteSpace(moviePath))
      {
        return null;
      }

      string normalized = MediaPathNormalizer.Normalize(moviePath);
      return records.FirstOrDefault(rec =>
        !string.IsNullOrWhiteSpace(rec.Movie_Path)
        && string.Equals(
          MediaPathNormalizer.Normalize(rec.Movie_Path),
          normalized,
          StringComparison.OrdinalIgnoreCase));
    }

    public static string ResolveSourceMoviePath(MovieRecords bookmark, IEnumerable<MovieRecords> library)
    {
      if (bookmark == null)
      {
        return null;
      }

      if (!string.IsNullOrWhiteSpace(bookmark.Comment1))
      {
        return bookmark.Comment1;
      }

      if (library == null)
      {
        return null;
      }

      List<MovieRecords> matches = [.. library.Where(rec =>
        string.Equals(rec.Movie_Body, bookmark.Movie_Body, StringComparison.OrdinalIgnoreCase)
        || string.Equals(
          Path.GetFileNameWithoutExtension(rec.Movie_Name ?? string.Empty),
          bookmark.Movie_Body,
          StringComparison.OrdinalIgnoreCase))];

      if (matches.Count == 0)
      {
        return null;
      }

      if (!string.IsNullOrWhiteSpace(bookmark.Hash))
      {
        MovieRecords byHash = matches.FirstOrDefault(rec =>
          string.Equals(rec.Hash, bookmark.Hash, StringComparison.OrdinalIgnoreCase));
        if (byHash != null)
        {
          return byHash.Movie_Path;
        }
      }

      return matches.Count == 1 ? matches[0].Movie_Path : null;
    }
  }
}
