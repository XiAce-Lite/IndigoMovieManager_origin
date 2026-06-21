using System.Collections.ObjectModel;
using System.Data;
using IndigoMovieManager.ModelViews;
using IndigoMovieManager.Thumbnail;

namespace IndigoMovieManager.Services
{
    internal sealed class MovieListCoordinator
    {
        public sealed class ReloadResult
        {
            public List<MovieRecords> Records { get; init; }
        }

        public sealed class FilterApplyResult
        {
            public IReadOnlyList<MovieRecords> Items { get; init; }
            public int SearchCount { get; init; }
        }

        public async Task<ReloadResult> ReloadAsync(
            string dbFullPath,
            string sortId,
            ThumbnailLayoutCache cache,
            int tabCount,
            int tabIndex)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return new ReloadResult { Records = [] };
            }

            string sql = MovieRecordQueries.SelectListOrdered(SortDefinitions.GetSqlOrderClause(sortId));

            return await Task.Run(() =>
            {
                using var session = new SQLiteSession(dbFullPath);
                DataTable table = session.Query(sql);
                List<MovieRecords> records = MovieRecordMapper.MapAll(table, cache, tabCount, tabIndex);
                return new ReloadResult { Records = records };
            }).ConfigureAwait(false);
        }

        public static FilterApplyResult ApplyFilter(
            IReadOnlyList<MovieRecords> source,
            string searchKeyword,
            string sortId)
        {
            MovieListFilter.FilterResult result = MovieListFilter.Build(source, searchKeyword, sortId);
            return new FilterApplyResult
            {
                Items = result.Items,
                SearchCount = result.SearchCount
            };
        }

        public static void ReplaceCollection(ObservableCollection<MovieRecords> target, IEnumerable<MovieRecords> records)
        {
            target.Clear();
            foreach (MovieRecords record in records)
            {
                target.Add(record);
            }
        }
    }
}
