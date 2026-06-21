namespace IndigoMovieManager.Services
{
    internal static class MovieFileInfoHelper
    {
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
    }
}
