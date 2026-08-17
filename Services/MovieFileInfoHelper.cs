using System.IO;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal static class MovieFileInfoHelper
    {
        public static long ToMovieSizeKb(long byteLength)
        {
            if (byteLength <= 0)
            {
                return 0;
            }

            return byteLength / 1024;
        }

        public static bool TryGetMovieSizeKb(string moviePath, out long sizeKb)
        {
            sizeKb = 0;
            if (string.IsNullOrWhiteSpace(moviePath) || !File.Exists(moviePath))
            {
                return false;
            }

            try
            {
                sizeKb = ToMovieSizeKb(new FileInfo(moviePath).Length);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void ApplyMovieSizeToRecord(MovieRecords rec, long sizeKb)
        {
            if (rec == null)
            {
                return;
            }

            rec.Movie_Size = sizeKb;
        }

        public static long GetMovieLengthSeconds(MovieRecords rec)
        {
            if (rec == null || string.IsNullOrEmpty(rec.Movie_Length))
            {
                return 0;
            }

            if (TimeSpan.TryParse(rec.Movie_Length, out TimeSpan parsed))
            {
                return (long)parsed.TotalSeconds;
            }

            return 0;
        }

        public static void ApplyFileInfoToRecord(MovieRecords rec, SinkuMetadata metadata, long existingMovieLengthSec)
        {
            rec.Container = metadata.Container ?? "";
            rec.Video = metadata.Video ?? "";
            rec.Audio = metadata.Audio ?? "";
            rec.Extra = metadata.Extra ?? "";

            if (existingMovieLengthSec < 1 && metadata.MovieLengthSec > 0)
            {
                rec.Movie_Length = new TimeSpan(0, 0, (int)metadata.MovieLengthSec).ToString(@"hh\:mm\:ss");
            }
        }

        public static void ApplyZipInfoToRecord(MovieRecords rec, int imageCount)
        {
            if (rec == null)
            {
                return;
            }

            rec.Container = "zip";
            rec.Video = "";
            rec.Audio = "";
            rec.Extra = "";
            rec.Movie_Length = imageCount > 0 ? $"{imageCount}枚" : "0枚";
        }
    }
}
