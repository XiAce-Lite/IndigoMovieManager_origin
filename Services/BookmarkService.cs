using System.Collections.ObjectModel;
using System.Data;
using IndigoMovieManager.ModelViews;

namespace IndigoMovieManager.Services
{
    internal static class BookmarkService
    {
        public static void LoadInto(
            DataTable bookmarkData,
            ObservableCollection<MovieRecords> target,
            string bookmarkFolder,
            string dbName)
        {
            if (bookmarkData == null)
            {
                return;
            }

            target.Clear();
            string resolvedFolder = BookmarkRecordMapper.ResolveBookmarkFolder(bookmarkFolder, dbName);

            foreach (DataRow row in bookmarkData.AsEnumerable())
            {
                target.Add(BookmarkRecordMapper.FromDataRow(row, resolvedFolder));
            }
        }

        /// <summary>
        /// Comment1 が空の古いブックマークを、ライブラリで一意に特定できたものだけ DB へ書き戻す。
        /// </summary>
        public static int BackfillMissingSources(
            IEnumerable<MovieRecords> bookmarks,
            IEnumerable<MovieRecords> library,
            Action<long, string, string> persist)
        {
            if (bookmarks == null)
            {
                return 0;
            }

            int count = 0;
            foreach (MovieRecords bookmark in bookmarks)
            {
                if (!BookmarkSourceResolver.TryBackfillFromLibrary(bookmark, library))
                {
                    continue;
                }

                persist?.Invoke(bookmark.Movie_Id, bookmark.Comment1, bookmark.Hash);
                count++;
            }

            return count;
        }
    }
}
