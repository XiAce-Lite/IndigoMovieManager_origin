namespace IndigoMovieManager.Services.Dmm
{
    internal static class DmmCandidateDisplay
    {
        public static string FormatMakerLabelSeries(DmmItemDto item)
        {
            if (item?.ItemInfo == null)
            {
                return string.Empty;
            }

            static string Join(IEnumerable<DmmNamedEntity> entities) =>
                string.Join(" / ", entities?
                    .Where(entity => !string.IsNullOrWhiteSpace(entity?.Name))
                    .Select(entity => entity.Name.Trim()) ?? []);

            string maker = Join(item.ItemInfo.Maker);
            string label = Join(item.ItemInfo.Label);
            string series = Join(item.ItemInfo.Series);

            return string.Join(" / ", new[] { maker, label, series }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        /// <summary>Large URL があり、now_printing 等のプレースホルダでない。</summary>
        public static bool HasUsableJacket(DmmItemDto item)
        {
            string url = item?.ImageUrl?.Large?.Trim() ?? string.Empty;
            if (!DmmJacketUrls.IsHttpUrl(url))
            {
                return false;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                && !DmmJacketUrls.IsPlaceholderJacketUri(uri);
        }
    }

    internal sealed class DmmCandidateRow
    {
        public string Title { get; init; }
        public string ContentId { get; init; }
        public string MakerLabelSeries { get; init; }
        public string FloorLabel { get; init; }
        public string JacketLabel { get; init; }
        public bool HasJacket { get; init; }
        public bool MatchesProductCode { get; init; }
        public DmmItemDto Item { get; init; }

        public static DmmCandidateRow FromEntry(DmmCandidateEntry entry, string productCode = null)
        {
            if (entry?.Item == null)
            {
                return new DmmCandidateRow
                {
                    FloorLabel = entry?.FloorLabel ?? string.Empty,
                    JacketLabel = "×",
                    HasJacket = false,
                    MatchesProductCode = false,
                };
            }

            bool hasJacket = DmmCandidateDisplay.HasUsableJacket(entry.Item);
            bool matches = !string.IsNullOrWhiteSpace(productCode)
                && DmmProductCodeMatcher.ItemMatchesProductCode(entry.Item, productCode);
            return new DmmCandidateRow
            {
                Title = entry.Item.Title?.Trim() ?? string.Empty,
                ContentId = entry.Item.ContentId?.Trim() ?? string.Empty,
                MakerLabelSeries = DmmCandidateDisplay.FormatMakerLabelSeries(entry.Item),
                FloorLabel = entry.FloorLabel ?? string.Empty,
                JacketLabel = hasJacket ? "○" : "×",
                HasJacket = hasJacket,
                MatchesProductCode = matches,
                Item = entry.Item,
            };
        }

        public static List<DmmCandidateRow> FromEntries(
            IEnumerable<DmmCandidateEntry> entries,
            string productCode = null)
        {
            if (entries == null)
            {
                return [];
            }

            // 品番一致＋ジャケ → 品番一致 → ジャケあり → その他（同順位は元の並びを維持）
            return [.. entries
                .Select(entry => FromEntry(entry, productCode))
                .Select((row, index) => (row, index))
                .OrderBy(x => SortRank(x.row))
                .ThenBy(x => x.index)
                .Select(x => x.row)];
        }

        /// <summary>品番一致かつジャケありを優先。無ければ先頭（単一候補含む）。</summary>
        public static DmmCandidateRow PreferSelection(
            IReadOnlyList<DmmCandidateRow> rows,
            string productCode = null)
        {
            if (rows == null || rows.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(productCode))
            {
                DmmCandidateRow matchWithJacket = rows.FirstOrDefault(r => r.MatchesProductCode && r.HasJacket);
                if (matchWithJacket != null)
                {
                    return matchWithJacket;
                }

                DmmCandidateRow matchOnly = rows.FirstOrDefault(r => r.MatchesProductCode);
                if (matchOnly != null)
                {
                    return matchOnly;
                }
            }

            return rows.FirstOrDefault(r => r.HasJacket) ?? rows[0];
        }

        private static int SortRank(DmmCandidateRow row)
        {
            if (row.MatchesProductCode && row.HasJacket)
            {
                return 0;
            }

            if (row.MatchesProductCode)
            {
                return 1;
            }

            if (row.HasJacket)
            {
                return 2;
            }

            return 3;
        }
    }
}
