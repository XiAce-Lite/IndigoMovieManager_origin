using IndigoMovieManager.Data;

namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmMetadataApplyService
    {
        public sealed class ApplySummary
        {
            public bool WroteComment1 { get; set; }
            public bool WroteComment2 { get; set; }
            public bool WroteComment3 { get; set; }
            public bool WroteTitle { get; set; }
            public bool WroteGenre { get; set; }
            public int AddedTagCount { get; set; }
        }

        public ApplySummary Apply(
            string dbFullPath,
            MovieRecords rec,
            DmmItemDto item,
            Action<Action> runOnUi = null)
        {
            ArgumentNullException.ThrowIfNull(rec);
            ArgumentNullException.ThrowIfNull(item);

            var summary = new ApplySummary();
            string title = item.Title?.Trim() ?? "";
            string comment2 = JoinNames(
                CollectNames(item.ItemInfo?.Maker),
                CollectNames(item.ItemInfo?.Label),
                CollectNames(item.ItemInfo?.Series));
            string affiliateUrl = item.AffiliateUrl?.Trim() ?? "";
            List<string> actresses = CollectNames(item.ItemInfo?.Actress);
            List<string> genres = CollectNames(item.ItemInfo?.Genre);
            string genreJoined = string.Join(" / ", genres);

            if (IsBlank(rec.Comment1) && !string.IsNullOrEmpty(title))
            {
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Comment1, title);
                RunUi(runOnUi, () => rec.Comment1 = title);
                summary.WroteComment1 = true;
            }

            if (IsBlank(rec.Comment2) && !string.IsNullOrEmpty(comment2))
            {
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Comment2, comment2);
                RunUi(runOnUi, () => rec.Comment2 = comment2);
                summary.WroteComment2 = true;
            }

            if (IsBlank(rec.Comment3) && !string.IsNullOrEmpty(affiliateUrl))
            {
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Comment3, affiliateUrl);
                RunUi(runOnUi, () => rec.Comment3 = affiliateUrl);
                summary.WroteComment3 = true;
            }

            if (IsBlank(rec.Title) && !string.IsNullOrEmpty(title))
            {
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Title, title);
                RunUi(runOnUi, () => rec.Title = title);
                summary.WroteTitle = true;
            }

            if (IsBlank(rec.Genre) && !string.IsNullOrEmpty(genreJoined))
            {
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Genre, genreJoined);
                RunUi(runOnUi, () => rec.Genre = genreJoined);
                summary.WroteGenre = true;
            }

            List<string> tagsToAdd = [];
            HashSet<string> existing = new(StringComparer.OrdinalIgnoreCase);
            if (rec.Tag != null)
            {
                foreach (string t in rec.Tag)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        existing.Add(t.Trim());
                    }
                }
            }

            void Consider(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                string trimmed = name.Trim();
                if (existing.Add(trimmed))
                {
                    tagsToAdd.Add(trimmed);
                }
            }

            foreach (string name in actresses)
            {
                Consider(name);
            }

            foreach (string name in genres)
            {
                Consider(name);
            }

            if (tagsToAdd.Count > 0)
            {
                string added = string.Join(Environment.NewLine, tagsToAdd);
                RunUi(runOnUi, () => TagMutationService.ApplyAdd(rec, added));
                WriteColumn(dbFullPath, rec.Movie_Id, MovieColumn.Tag, rec.Tags);
                summary.AddedTagCount = tagsToAdd.Count;
            }

            return summary;
        }

        private static void WriteColumn(string dbFullPath, long movieId, MovieColumn column, object value)
        {
            if (string.IsNullOrEmpty(dbFullPath))
            {
                return;
            }

            SQLite.UpdateMovieSingleColumn(dbFullPath, movieId, column, value);
        }

        private static void RunUi(Action<Action> runOnUi, Action action)
        {
            if (runOnUi != null)
            {
                runOnUi(action);
                return;
            }

            action();
        }

        private static bool IsBlank(string value) => string.IsNullOrWhiteSpace(value);

        private static List<string> CollectNames(IEnumerable<DmmNamedEntity> entities)
        {
            var list = new List<string>();
            if (entities == null)
            {
                return list;
            }

            foreach (DmmNamedEntity entity in entities)
            {
                if (!string.IsNullOrWhiteSpace(entity?.Name))
                {
                    list.Add(entity.Name.Trim());
                }
            }

            return list;
        }

        private static string JoinNames(params IEnumerable<string>[] groups)
        {
            var parts = new List<string>();
            foreach (IEnumerable<string> group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (string name in group)
                {
                    if (!string.IsNullOrWhiteSpace(name) && !parts.Contains(name, StringComparer.Ordinal))
                    {
                        parts.Add(name);
                    }
                }
            }

            return string.Join(" / ", parts);
        }
    }
}
