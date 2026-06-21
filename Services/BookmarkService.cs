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
    }
}
