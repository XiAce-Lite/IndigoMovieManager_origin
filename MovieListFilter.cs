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

            filterList = ApplySort(filterList, sortId);
            List<MovieRecords> items = filterList.ToList();

            return new FilterResult
            {
                Items = items,
                SearchCount = searchCount
            };
        }

        private static IEnumerable<MovieRecords> ApplySort(IEnumerable<MovieRecords> filterList, string id)
        {
            return id switch
            {
                "0" => from x in filterList orderby x.Last_Date descending select x,
                "1" => from x in filterList orderby x.Last_Date select x,
                "2" => from x in filterList orderby x.File_Date descending select x,
                "3" => from x in filterList orderby x.File_Date select x,
                "6" => from x in filterList orderby x.Score descending select x,
                "7" => from x in filterList orderby x.Score select x,
                "8" => from x in filterList orderby x.View_Count descending select x,
                "9" => from x in filterList orderby x.View_Count select x,
                "10" => from x in filterList orderby x.Kana select x,
                "11" => from x in filterList orderby x.Kana descending select x,
                "12" => from x in filterList orderby x.Movie_Name select x,
                "13" => from x in filterList orderby x.Movie_Name descending select x,
                "14" => from x in filterList orderby x.Movie_Path select x,
                "15" => from x in filterList orderby x.Movie_Path descending select x,
                "16" => from x in filterList orderby x.Movie_Size descending select x,
                "17" => from x in filterList orderby x.Movie_Size select x,
                "18" => from x in filterList orderby x.Regist_Date descending select x,
                "19" => from x in filterList orderby x.Regist_Date select x,
                "20" => from x in filterList orderby x.Movie_Length descending select x,
                "21" => from x in filterList orderby x.Movie_Length select x,
                "22" => from x in filterList orderby x.Comment1 select x,
                "23" => from x in filterList orderby x.Comment1 descending select x,
                "24" => from x in filterList orderby x.Comment2 select x,
                "25" => from x in filterList orderby x.Comment2 descending select x,
                "26" => from x in filterList orderby x.Comment3 select x,
                "27" => from x in filterList orderby x.Comment3 descending select x,
                _ => filterList,
            };
        }
    }
}
