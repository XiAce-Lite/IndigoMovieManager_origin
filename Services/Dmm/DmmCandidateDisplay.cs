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
    }

    internal sealed class DmmCandidateRow
    {
        public string Title { get; init; }
        public string ContentId { get; init; }
        public string MakerLabelSeries { get; init; }
        public string FloorLabel { get; init; }
        public string JacketLabel { get; init; }
        public DmmItemDto Item { get; init; }

        public static DmmCandidateRow FromEntry(DmmCandidateEntry entry)
        {
            if (entry?.Item == null)
            {
                return new DmmCandidateRow
                {
                    FloorLabel = entry?.FloorLabel ?? string.Empty,
                    JacketLabel = "×",
                };
            }

            string jacketUrl = entry.Item.ImageUrl?.Large?.Trim() ?? string.Empty;
            return new DmmCandidateRow
            {
                Title = entry.Item.Title?.Trim() ?? string.Empty,
                ContentId = entry.Item.ContentId?.Trim() ?? string.Empty,
                MakerLabelSeries = DmmCandidateDisplay.FormatMakerLabelSeries(entry.Item),
                FloorLabel = entry.FloorLabel ?? string.Empty,
                JacketLabel = string.IsNullOrEmpty(jacketUrl) ? "×" : "○",
                Item = entry.Item,
            };
        }

        public static List<DmmCandidateRow> FromEntries(IEnumerable<DmmCandidateEntry> entries) =>
            entries?.Select(FromEntry).ToList() ?? [];
    }
}
