using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal static class FileInfoRefreshService
    {
        public static void RefreshCore(
            string dbPath,
            MovieRecords rec,
            Action<Action> runOnUi)
        {
            if (rec == null || string.IsNullOrWhiteSpace(rec.Movie_Path))
            {
                return;
            }

            TryRefreshMovieSize(dbPath, rec, runOnUi);

            if (ZipMediaKind.IsZipRecord(rec) || ZipMediaKind.IsZipPath(rec.Movie_Path))
            {
                RefreshZipCore(dbPath, rec, runOnUi);
                return;
            }

            if (!SinkuMetadataFetcher.TryFetch(rec.Movie_Path, out SinkuMetadata metadata))
            {
                return;
            }

            long existingSec = MovieFileInfoHelper.GetMovieLengthSeconds(rec);
            SQLite.UpdateMovieFileInfo(dbPath, rec.Movie_Id, metadata, existingSec);
            runOnUi(() => MovieFileInfoHelper.ApplyFileInfoToRecord(rec, metadata, existingSec));
        }

        public static void RefreshZipCore(
            string dbPath,
            MovieRecords rec,
            Action<Action> runOnUi)
        {
            if (rec == null || string.IsNullOrWhiteSpace(rec.Movie_Path))
            {
                return;
            }

            int imageCount = 0;
            if (ZipImageCatalog.TryGetImageEntries(rec.Movie_Path, out IReadOnlyList<string> entries))
            {
                imageCount = entries.Count;
            }

            SQLite.UpdateMovieZipInfo(dbPath, rec.Movie_Id, imageCount);
            runOnUi(() => MovieFileInfoHelper.ApplyZipInfoToRecord(rec, imageCount));
        }

        private static void TryRefreshMovieSize(
            string dbPath,
            MovieRecords rec,
            Action<Action> runOnUi)
        {
            if (!MovieFileInfoHelper.TryGetMovieSizeKb(rec.Movie_Path, out long sizeKb))
            {
                return;
            }

            SQLite.UpdateMovieSingleColumn(dbPath, rec.Movie_Id, "movie_size", sizeKb);
            runOnUi(() => MovieFileInfoHelper.ApplyMovieSizeToRecord(rec, sizeKb));
        }
    }
}
