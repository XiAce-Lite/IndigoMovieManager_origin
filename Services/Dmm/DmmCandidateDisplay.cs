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
        public DmmItemDto Item { get; init; }

        public static DmmCandidateRow FromEntry(DmmCandidateEntry entry)
        {
            if (entry?.Item == null)
            {
                return new DmmCandidateRow
                {
                    FloorLabel = entry?.FloorLabel ?? string.Empty,
                    JacketLabel = "×",
                    HasJacket = false,
                };
            }

            bool hasJacket = DmmCandidateDisplay.HasUsableJacket(entry.Item);
            return new DmmCandidateRow
            {
                Title = entry.Item.Title?.Trim() ?? string.Empty,
                ContentId = entry.Item.ContentId?.Trim() ?? string.Empty,
                MakerLabelSeries = DmmCandidateDisplay.FormatMakerLabelSeries(entry.Item),
                FloorLabel = entry.FloorLabel ?? string.Empty,
                JacketLabel = hasJacket ? "○" : "×",
                HasJacket = hasJacket,
                Item = entry.Item,
            };
        }

        public static List<DmmCandidateRow> FromEntries(IEnumerable<DmmCandidateEntry> entries)
        {
            if (entries == null)
            {
                return [];
            }

            // ジャケありを先頭へ（同順位は元の並びを維持）
            return [.. entries
                .Select(FromEntry)
                .Select((row, index) => (row, index))
                .OrderByDescending(x => x.row.HasJacket)
                .ThenBy(x => x.index)
                .Select(x => x.row)];
        }

        /// <summary>ジャケありを優先。無ければ先頭（単一候補含む）。</summary>
        public static DmmCandidateRow PreferSelection(IReadOnlyList<DmmCandidateRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return null;
            }

            return rows.FirstOrDefault(r => r.HasJacket) ?? rows[0];
        }
    }
}
