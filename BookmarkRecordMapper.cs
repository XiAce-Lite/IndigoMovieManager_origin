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
                IsExists = true,
                Ext = ext,
                ThumbDetail = thumbFile
            };
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
