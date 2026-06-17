namespace IndigoMovieManager
{
    internal static class MovieRecordQueries
    {
        public const string ListColumns =
            "movie_id, movie_name, movie_path, movie_length, movie_size, " +
            "last_date, file_date, regist_date, score, view_count, hash, " +
            "container, video, audio, extra, title, album, artist, grouping, " +
            "writer, genre, track, camera, create_time, kana, roma, tag, " +
            "comment1, comment2, comment3";

        public static string SelectListOrdered(string orderClause)
        {
            return $"SELECT {ListColumns} FROM movie ORDER BY {orderClause}";
        }
    }
}
