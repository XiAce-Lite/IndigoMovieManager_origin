using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager
{
    public class MovieInfo
    {
        private long movie_id = 0;
        private string movie_name = "";
        private string movie_path = "";
        private long movie_length = 0;
        private long movie_size = 0;
        private DateTime last_date = DateTime.Now;
        private DateTime file_date = DateTime.Now;
        private DateTime regist_date = DateTime.Now;
        private readonly long score = 0;
        private long view_count = 0;
        private string hash = "";
        private string container = "";
        private readonly string video = "";
        private readonly string audio = "";
        private readonly string extra = "";
        private readonly string title = "";
        private readonly string artist = "";
        private readonly string album = "";
        private readonly string grouping = "";
        private readonly string writer = "";
        private readonly string genre = "";
        private readonly string track = "";
        private readonly string camera = "";
        private readonly string create_time = "";
        private readonly string kana = "";
        private readonly string roma = "";
        private readonly string tag = "";
        private readonly string comment1 = "";
        private readonly string comment2 = "";
        private readonly string comment3 = "";
        private double fps = 30;
        private double totalFrames = 0;

        public MovieInfo(string fileFullPath, bool noHash = false)
        {
            if (string.IsNullOrWhiteSpace(fileFullPath))
            {
                throw new ArgumentException("ファイルパスが空です。", nameof(fileFullPath));
            }

            FileInfo file = new(fileFullPath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("ファイルが見つかりません。", fileFullPath);
            }

            var now = DateTime.Now;
            var result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
            last_date = result;
            regist_date = result;

            movie_name = Path.GetFileNameWithoutExtension(fileFullPath);
            movie_path = file.FullName;
            movie_size = file.Length;

            var lastWrite = file.LastWriteTime;
            result = lastWrite.AddTicks(-(lastWrite.Ticks % TimeSpan.TicksPerSecond));
            file_date = result;

            if (!noHash)
            {
                hash = Tools.GetHashCRC32(fileFullPath);
            }

            if (ZipMediaKind.IsZipPath(fileFullPath))
            {
                container = "zip";
                if (ZipImageCatalog.TryGetImageEntries(fileFullPath, out IReadOnlyList<string> entries))
                {
                    movie_length = entries.Count;
                }

                return;
            }

            if (!ShouldSkipOpenCvProbe(fileFullPath))
            {
                TryProbeWithOpenCv(fileFullPath);
            }
        }

        private static bool ShouldSkipOpenCvProbe(string fileFullPath)
        {
            string ext = Path.GetExtension(fileFullPath);
            return ext.Equals(".mod", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private void TryProbeWithOpenCv(string fileFullPath)
        {
            try
            {
                using VideoCapture capture = new(fileFullPath);
                if (!capture.IsOpened())
                {
                    return;
                }

                capture.Grab();
                double frameCount = capture.Get(VideoCaptureProperties.FrameCount);
                double captureFps = capture.Get(VideoCaptureProperties.Fps);
                if (captureFps <= 0
                    || frameCount <= 0
                    || double.IsNaN(captureFps)
                    || double.IsInfinity(captureFps)
                    || double.IsNaN(frameCount)
                    || double.IsInfinity(frameCount))
                {
                    return;
                }

                totalFrames = frameCount;
                fps = captureFps;
                movie_length = (long)(totalFrames / fps);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [MovieInfo] OpenCV probe skipped: {fileFullPath} : {ex.Message}");
            }
        }

        public long MovieId { get { return movie_id; } set { movie_id = value; } }
        public string MovieName { get { return movie_name; } set { movie_name = value; } }
        public string MoviePath { get { return movie_path; } set { movie_path = value; } }
        public long MovieLength { get { return movie_length; } }
        public long MovieSize { get { return movie_size; } }
        public DateTime LastDate { get { return last_date; } set { last_date = value; } }
        public DateTime FileDate { get { return file_date; } set { file_date = value; } }
        public DateTime RegistDate { get { return regist_date; } set { regist_date = value; } }
        public long Score { get { return score; } }
        public long ViewCount { get { return view_count; } set { view_count = value; } }
        public string Hash { get { return hash; } }
        public string Container { get { return container; } }
        public string Video { get { return video; } }
        public string Audio { get { return audio; } }
        public string Extra { get { return extra; } }
        public string Title { get { return title; } }
        public string Artist { get { return artist; } }
        public string Album { get { return album; } }
        public string Grouping { get { return grouping; } }
        public string Writer { get { return writer; } }
        public string Genre { get { return genre; } }
        public string Track { get { return track; } }
        public string Camera { get { return camera; } }
        public string CreateTime { get { return create_time; } }
        public string Kana { get { return kana; } }
        public string Roma { get { return roma; } }
        public string Tag { get { return tag; } }
        public string Comment1 { get { return comment1; } }
        public string Comment2 { get { return comment2; } }
        public string Comment3 { get { return comment3; } }
        public double FPS { get { return fps; } }
        public double TotalFrames { get { return totalFrames; } }
    }
}
