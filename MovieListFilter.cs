using IndigoMovieManager.Services;

namespace IndigoMovieManager
{
    internal static class MovieListFilter
    {
        internal sealed class FilterResult
        {
            public IReadOnlyList<MovieRecords> Items { get; init; }
            public int SearchCount { get; init; }
            public string OverrideSortId { get; init; }
        }

        public static FilterResult Build(
            IReadOnlyList<MovieRecords> source,
            string searchKeyword,
            string sortId,
            MovieListFilterContext context = null)
        {
            IEnumerable<MovieRecords> filterList = source;
            int searchCount = source.Count;

            if (!string.IsNullOrEmpty(searchKeyword))
            {
                string searchText = searchKeyword.Trim();

                if ((searchText.Length >= 2) &&
                    ((searchText.StartsWith('"') && searchText.EndsWith('"')) ||
                     (searchText.StartsWith('\'') && searchText.EndsWith('\''))))
                {
                    string exact = searchText[1..^1];
                    filterList = filterList.Where(item =>
                        (item.Movie_Name ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase) ||
                        (item.Movie_Path ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase) ||
                        (item.Tags ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase) ||
                        (item.Title ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase)
                    );
                    searchCount = filterList.Count();
                }
                else if (searchText.StartsWith('{') && searchText.EndsWith('}'))
                {
                    string inner = searchText[1..^1].Trim();
                    string effectiveSortId = sortId;
                    if (WhiteBrowserBraceSearch.TryApply(
                            source,
                            inner,
                            context,
                            out IReadOnlyList<MovieRecords> braceFiltered,
                            out string overrideSortId))
                    {
                        filterList = braceFiltered;
                        searchCount = filterList.Count();
                        if (!string.IsNullOrEmpty(overrideSortId))
                        {
                            effectiveSortId = overrideSortId;
                        }
                    }

                    filterList = SortDefinitions.Apply(effectiveSortId, filterList);
                    List<MovieRecords> braceItems = [.. filterList];
                    return new FilterResult
                    {
                        Items = braceItems,
                        SearchCount = searchCount,
                        OverrideSortId = effectiveSortId,
                    };
                }
                else
                {
                    string[] orGroups = searchText.Split([" | "], StringSplitOptions.RemoveEmptyEntries);

                    filterList = filterList.Where(item =>
                    {
                        return orGroups.Any(group =>
                        {
                            string[] andTerms = group.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                            return andTerms.All(term =>
                            {
                                if (term.StartsWith('!'))
                                {
                                    // WhiteBrowser 準拠のタグ検索（タグ単位で完全一致）。
                                    return MatchesTagExact(item, term[1..]);
                                }

                                if (term.StartsWith('-'))
                                {
                                    string keyword = term[1..];
                                    return !MatchesTerm(item, keyword);
                                }

                                return MatchesTerm(item, term);
                            });
                        });
                    });
                    searchCount = filterList.Count();
                }
            }

            filterList = SortDefinitions.Apply(sortId, filterList);
            List<MovieRecords> items = [.. filterList];

            return new FilterResult
            {
                Items = items,
                SearchCount = searchCount
            };
        }

        // ファイル名・パス・タイトルは部分一致、タグはタグ単位で完全一致。
        // genre / artist / comment1–3 は通常検索対象外（{} SQL または詳細クリック生成で列指定）。
        private static bool MatchesTerm(MovieRecords item, string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return false;
            }

            string[] textFields =
            [
                item.Movie_Name ?? "",
                item.Movie_Path ?? "",
                item.Title ?? "",
            ];

            if (textFields.Any(f => f.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            {
                return true;
            }

            return MatchesTagExact(item, term);
        }

        // タグ単位の完全一致。「★」で「★★」がヒットしないようにする。
        private static bool MatchesTagExact(MovieRecords item, string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            if (item.Tag is { } tagList)
            {
                foreach (string t in tagList)
                {
                    if (string.Equals(t?.Trim(), tag, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
