using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using IndigoMovieManager.Services;
using IndigoMovieManager.Services.WpfSkin;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
    internal static partial class MovieRecordMapper
    {
        [GeneratedRegex(@"^\r\n+")]
        private static partial Regex TagLeadingNewlinesRegex();

        public static MovieRecords FromDataRow(
            DataRow row,
            ThumbnailLayoutCache cache,
            SkinEngine? resolveEngineOnly = null)
        {
            string hash = row["hash"].ToString();
            string movieFullPath = row["movie_path"].ToString();
            string thumbFile = ThumbnailLayoutCache.GetThumbFileName(row["movie_name"].ToString(), hash);

            bool resolveAll = resolveEngineOnly == null;
            string thumbPathWpf = null;
            string thumbPathWb = null;
            string thumbPathDetail = null;

            if (resolveAll || resolveEngineOnly == SkinEngine.Wpf)
            {
                ThumbnailLayoutSpec wpfSpec = WpfSkinSettings.CurrentThumbnailLayout
                    ?? new ThumbnailLayoutSpec(400, 225, 1, 1);
                thumbPathWpf = cache.BuildThumbPath(wpfSpec, thumbFile, checkExists: true);
            }

            if (resolveAll || resolveEngineOnly == SkinEngine.Wb)
            {
                ThumbnailLayoutSpec wbSpec = WhiteBrowserSkinSettings.GetThumbnailLayoutSpec();
                thumbPathWb = cache.BuildThumbPath(wbSpec, thumbFile, checkExists: true);
            }

            if (resolveAll)
            {
                thumbPathDetail = cache.ResolveDetailThumbPath(thumbFile, checkExists: true);
            }

            string tags = row["tag"].ToString();
            List<string> tagArray = [];
            if (!string.IsNullOrEmpty(tags))
            {
                foreach (var tagItem in tags.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                {
                    tagArray.Add(tagItem);
                }
            }

            string tag = TagLeadingNewlinesRegex().Replace(tags, "");
            string ext = Path.GetExtension(movieFullPath);
            string movieBody = Path.GetFileNameWithoutExtension(movieFullPath);
            string containerValue = row["container"].ToString();
            long movieLengthRaw = (long)row["movie_length"];
            string movieLength = string.Equals(containerValue, "zip", StringComparison.OrdinalIgnoreCase)
                ? $"{movieLengthRaw}枚"
                : new TimeSpan(0, 0, (int)movieLengthRaw).ToString(@"hh\:mm\:ss");

            return new MovieRecords
            {
                Movie_Id = (long)row["movie_id"],
                Movie_Name = $"{row["movie_name"]}{ext}",
                Movie_Body = movieBody,
                Movie_Path = row["movie_path"].ToString(),
                Movie_Length = movieLength,
                Movie_Size = (long)row["movie_size"],
                Last_Date = ((DateTime)row["last_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                File_Date = ((DateTime)row["file_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                Regist_Date = ((DateTime)row["regist_date"]).ToString("yyyy-MM-dd HH:mm:ss"),
                Score = (long)row["score"],
                View_Count = (long)row["view_count"],
                Hash = hash,
                Container = containerValue,
                Video = row["video"].ToString(),
                Audio = row["audio"].ToString(),
                Extra = row["extra"].ToString(),
                Title = row["title"].ToString(),
                Album = row["album"].ToString(),
                Artist = row["artist"].ToString(),
                Grouping = row["grouping"].ToString(),
                Writer = row["writer"].ToString(),
                Genre = row["genre"].ToString(),
                Track = row["track"].ToString(),
                Camera = row["camera"].ToString(),
                Create_Time = row["create_time"].ToString(),
                Kana = row["kana"].ToString(),
                Roma = row["roma"].ToString(),
                Tags = tag,
                Tag = tagArray,
                Comment1 = row["comment1"].ToString(),
                Comment2 = row["comment2"].ToString(),
                Comment3 = row["comment3"].ToString(),
                ThumbPathWpfSkin = thumbPathWpf,
                ThumbPathWb = thumbPathWb,
                ThumbDetail = thumbPathDetail,
                Drive = Path.GetPathRoot(row["movie_path"].ToString()),
                Dir = Path.GetDirectoryName(row["movie_path"].ToString()),
                IsExists = Path.Exists(movieFullPath),
                Ext = ext
            };
        }

        public static List<MovieRecords> MapAll(
            DataTable movieTable,
            ThumbnailLayoutCache cache,
            SkinEngine? resolveEngineOnly = null)
        {
            var records = new List<MovieRecords>(movieTable.Rows.Count);
            foreach (DataRow row in movieTable.Rows)
            {
                records.Add(FromDataRow(row, cache, resolveEngineOnly));
            }

            return records;
        }
    }
}
