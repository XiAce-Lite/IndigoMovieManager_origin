namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// ジャケあり件数に基づく打ち切り判定（自動 Resolve / 手動検索共通）。
    /// 自動適用は「品番一致かつジャケあり」がちょうど1件のときだけ。
    /// </summary>
    internal static class DmmJacketHitEvaluator
    {
        public static int CountUsableJackets(IEnumerable<DmmCandidateEntry> candidates)
        {
            if (candidates == null)
            {
                return 0;
            }

            int count = 0;
            foreach (DmmCandidateEntry entry in candidates)
            {
                if (DmmCandidateDisplay.HasUsableJacket(entry?.Item))
                {
                    count++;
                }
            }

            return count;
        }

        public static bool HasAnyUsableJacket(IEnumerable<DmmCandidateEntry> candidates) =>
            CountUsableJackets(candidates) > 0;

        public static bool HasProductMatchingUsableJacket(
            IEnumerable<DmmCandidateEntry> candidates,
            string productCode) =>
            CountProductMatchingUsableJackets(candidates, productCode) > 0;

        /// <summary>
        /// 品番一致かつジャケありの ContentId ユニーク件数。
        /// </summary>
        public static int CountProductMatchingUsableJackets(
            IEnumerable<DmmCandidateEntry> candidates,
            string productCode)
        {
            return CollectProductMatchingUsableJackets(candidates, productCode).Count;
        }

        /// <summary>
        /// ジャケありかつ品番一致が1件（ContentId 単位）→ Applied、
        /// 2件以上 → Ambiguous、
        /// 0件 → null（検索継続）。
        /// Ambiguous 時は一覧全体（無関係・ジャケなし含む）を渡す。
        /// </summary>
        public static DmmResolveResult TryConclude(
            IReadOnlyList<DmmCandidateEntry> candidates,
            string productCode)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            List<DmmCandidateEntry> matchingJackets =
                CollectProductMatchingUsableJackets(candidates, productCode);

            if (matchingJackets.Count == 1)
            {
                return DmmResolveResult.Applied(matchingJackets[0].Item, productCode);
            }

            if (matchingJackets.Count >= 2)
            {
                return DmmResolveResult.Ambiguous(
                    candidates,
                    productCode,
                    productCode);
            }

            return null;
        }

        private static List<DmmCandidateEntry> CollectProductMatchingUsableJackets(
            IEnumerable<DmmCandidateEntry> candidates,
            string productCode)
        {
            var result = new List<DmmCandidateEntry>();
            if (candidates == null || string.IsNullOrWhiteSpace(productCode))
            {
                return result;
            }

            var seenContentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DmmCandidateEntry entry in candidates)
            {
                if (entry?.Item == null
                    || !DmmCandidateDisplay.HasUsableJacket(entry.Item)
                    || !DmmProductCodeMatcher.ItemMatchesProductCode(entry.Item, productCode))
                {
                    continue;
                }

                string contentId = entry.Item.ContentId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(contentId) && !seenContentIds.Add(contentId))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(contentId))
                {
                    // ContentId 無しは重複排除できないため個別に数える
                    result.Add(entry);
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }
    }
}
