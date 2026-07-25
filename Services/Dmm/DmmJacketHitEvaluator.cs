namespace IndigoMovieManager.Services.Dmm
{
    /// <summary>
    /// ジャケあり件数に基づく打ち切り判定（自動 Resolve / 手動検索共通）。
    /// ジャケ1件でも要求品番と maker+番号が一致しない候補は自動適用しない。
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

        /// <summary>
        /// ジャケあり1件かつ品番一致 → Applied、
        /// ジャケあり1件だが品番不一致 → Ambiguous、
        /// ジャケあり2件以上 → Ambiguous、
        /// 0件 → null（継続）。
        /// Ambiguous 時は一覧全体（ジャケなし含む）を渡す。
        /// </summary>
        public static DmmResolveResult TryConclude(
            IReadOnlyList<DmmCandidateEntry> candidates,
            string productCode)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            var withJacket = new List<DmmCandidateEntry>();
            foreach (DmmCandidateEntry entry in candidates)
            {
                if (DmmCandidateDisplay.HasUsableJacket(entry?.Item))
                {
                    withJacket.Add(entry);
                }
            }

            if (withJacket.Count == 1)
            {
                DmmCandidateEntry only = withJacket[0];
                if (DmmProductCodeMatcher.ItemMatchesProductCode(only.Item, productCode))
                {
                    return DmmResolveResult.Applied(only.Item, productCode);
                }

                // 類似品番のジャケ1件だけでは誤登録するため、候補選択へ回す
                return DmmResolveResult.Ambiguous(
                    candidates,
                    productCode,
                    productCode);
            }

            if (withJacket.Count >= 2)
            {
                return DmmResolveResult.Ambiguous(
                    candidates,
                    productCode,
                    productCode);
            }

            return null;
        }
    }
}
