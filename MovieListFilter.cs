namespace IndigoMovieManager
{
    internal static class MovieListFilter
    {
        internal sealed class FilterResult
        {
            public IReadOnlyList<MovieRecords> Items { get; init; }
            public int SearchCount { get; init; }
        }

        public static FilterResult Build(IReadOnlyList<MovieRecords> source, string searchKeyword, string sortId)
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
                        (item.Comment1 ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase) ||
                        (item.Comment2 ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase) ||
                        (item.Comment3 ?? "").Contains(exact, StringComparison.CurrentCultureIgnoreCase)
                    );
                    searchCount = filterList.Count();
                }
                else if (searchText.StartsWith('{') && searchText.EndsWith('}'))
                {
                    string inner = searchText[1..^1].Trim();

                    if (inner.Equals("notag", StringComparison.CurrentCultureIgnoreCase))
                    {
                        filterList = filterList.Where(x => string.IsNullOrEmpty(x.Tags));
                        searchCount = filterList.Count();
                    }
                    else if (inner.Equals("dup", StringComparison.CurrentCultureIgnoreCase))
                    {
                        HashSet<string> dupHashes = filterList
                            .GroupBy(x => x.Hash)
                            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
                            .Select(g => g.Key)
                            .ToHashSet();

                        filterList = filterList.Where(x => dupHashes.Contains(x.Hash));
                        searchCount = filterList.Count();
                    }
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
                                string[] fields =
                                [
                                    item.Movie_Name ?? "",
                                    item.Movie_Path ?? "",
                                    item.Tags ?? "",
                                    item.Comment1 ?? "",
                                    item.Comment2 ?? "",
                                    item.Comment3 ?? ""
                                ];

                                if (term.StartsWith('-'))
                                {
                                    string keyword = term[1..];
                                    return fields.All(f => !f.Contains(keyword, StringComparison.CurrentCultureIgnoreCase));
                                }

                                return fields.Any(f => f.Contains(term, StringComparison.CurrentCultureIgnoreCase));
                            });
                        });
                    });
                    searchCount = filterList.Count();
                }
            }

            filterList = SortDefinitions.Apply(sortId, filterList);
            List<MovieRecords> items = filterList.ToList();

            return new FilterResult
            {
                Items = items,
                SearchCount = searchCount
            };
        }
    }
}
