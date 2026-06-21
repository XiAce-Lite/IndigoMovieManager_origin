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

            if (!SinkuMetadataFetcher.TryFetch(rec.Movie_Path, out SinkuMetadata metadata))
            {
                return;
            }

            long existingSec = MovieFileInfoHelper.GetMovieLengthSeconds(rec);
            SQLite.UpdateMovieFileInfo(dbPath, rec.Movie_Id, metadata, existingSec);
            runOnUi(() => MovieFileInfoHelper.ApplyFileInfoToRecord(rec, metadata, existingSec));
        }
    }
}
