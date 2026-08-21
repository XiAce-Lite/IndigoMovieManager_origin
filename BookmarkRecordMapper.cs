using System.Data;
using System.IO;

namespace IndigoMovieManager
{
    internal static class BookmarkRecordMapper
    {
        public static MovieRecords FromDataRow(DataRow row, string bookmarkFolder)
        {
            var movieFullPath = row["movie_path"].ToString();
            var ext = Path.GetExtension(movieFullPath);
            var thumbFile = Path.Combine(bookmarkFolder, movieFullPath);
            var thumbBody = movieFullPath.Split('[')[0];
            var frameS = movieFullPath.Split('(')[1];
            frameS = frameS.Split(')')[0];
            long frame = 0;
            if (frameS != "")
            {
                frame = Convert.ToInt64(frameS);
            }

            return new MovieRecords
            {
                Movie_Id = (long)row["movie_id"],
                Movie_Name = $"{row["movie_name"]}{ext}",
                Movie_Body = thumbBody,
                Last_Date = ((DateTime)row["last_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                File_Date = ((DateTime)row["file_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                Regist_Date = ((DateTime)row["regist_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                View_Count = (long)row["view_count"],
                Score = frame,
                Kana = row["kana"].ToString(),
                Roma = row["roma"].ToString(),
                Hash = row["hash"]?.ToString() ?? "",
                Comment1 = row["comment1"]?.ToString() ?? "",
                IsExists = ResolveSourceExists(row["comment1"]?.ToString()),
                Ext = ext,
                ThumbDetail = thumbFile
            };
        }

        /// <summary>
        /// 元動画が特定でき、かつファイルが存在するときだけ true。
        /// Comment1 が空（古いブックマーク）やパス先が無い場合は false（一覧と同じ白黒表示）。
        /// </summary>
        public static bool ResolveSourceExists(string comment1)
        {
            if (string.IsNullOrWhiteSpace(comment1))
            {
                return false;
            }

            return Path.Exists(comment1);
        }

        public static string ResolveBookmarkFolder(string configuredFolder, string dbName)
        {
            if (!string.IsNullOrEmpty(configuredFolder))
            {
                return configuredFolder;
            }

            return Path.Combine(Directory.GetCurrentDirectory(), "bookmark", dbName);
        }
    }
}
