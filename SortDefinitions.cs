namespace IndigoMovieManager
{
    /// <summary>
    /// ソート ID の単一定義。SQL ORDER BY とメモリ上ソートの両方に使用する。
    /// </summary>
    internal static class SortDefinitions
    {
        /// <summary>ランダムソート（ID 28）用シード。コンボで「ランダム」を選んだときだけ更新する。</summary>
        public static int RandomSeed { get; private set; } = Environment.TickCount;

        public static void ReseedRandom() => RandomSeed = Random.Shared.Next();

        public static string GetSqlOrderClause(string id)
        {
            return id switch
            {
                "0" => "last_date desc",
                "1" => "last_date",
                "2" => "file_date desc",
                "3" => "file_date",
                "6" => "Score desc",
                "7" => "Score",
                "8" => "view_count desc",
                "9" => "view_count",
                "10" => "kana",
                "11" => "kana desc",
                "12" => "movie_name",
                "13" => "movie_name desc",
                "14" => "movie_path",
                "15" => "movie_path desc",
                "16" => "movie_size desc",
                "17" => "movie_size",
                "18" => "regist_date desc",
                "19" => "regist_date",
                "20" => "movie_length desc",
                "21" => "movie_length",
                "22" => "comment1",
                "23" => "comment1 desc",
                "24" => "comment2",
                "25" => "comment2 desc",
                "26" => "comment3",
                "27" => "comment3 desc",
                "28" => "movie_id",
                _ => "",
            };
        }

        public static IEnumerable<MovieRecords> Apply(string id, IEnumerable<MovieRecords> source)
        {
            return id switch
            {
                "0" => from x in source orderby x.Last_Date descending select x,
                "1" => from x in source orderby x.Last_Date select x,
                "2" => from x in source orderby x.File_Date descending select x,
                "3" => from x in source orderby x.File_Date select x,
                "6" => from x in source orderby x.Score descending select x,
                "7" => from x in source orderby x.Score select x,
                "8" => from x in source orderby x.View_Count descending select x,
                "9" => from x in source orderby x.View_Count select x,
                "10" => from x in source orderby x.Kana select x,
                "11" => from x in source orderby x.Kana descending select x,
                "12" => from x in source orderby x.Movie_Name select x,
                "13" => from x in source orderby x.Movie_Name descending select x,
                "14" => from x in source orderby x.Movie_Path select x,
                "15" => from x in source orderby x.Movie_Path descending select x,
                "16" => from x in source orderby x.Movie_Size descending select x,
                "17" => from x in source orderby x.Movie_Size select x,
                "18" => from x in source orderby x.Regist_Date descending select x,
                "19" => from x in source orderby x.Regist_Date select x,
                "20" => from x in source orderby x.Movie_Length descending select x,
                "21" => from x in source orderby x.Movie_Length select x,
                "22" => from x in source orderby x.Comment1 select x,
                "23" => from x in source orderby x.Comment1 descending select x,
                "24" => from x in source orderby x.Comment2 select x,
                "25" => from x in source orderby x.Comment2 descending select x,
                "26" => from x in source orderby x.Comment3 select x,
                "27" => from x in source orderby x.Comment3 descending select x,
                "28" => source.OrderBy(x => HashCode.Combine(RandomSeed, x.Movie_Id)),
                _ => source,
            };
        }
    }
}
