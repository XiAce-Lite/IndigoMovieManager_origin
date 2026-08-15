namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmMetadataResolveService
    {
        private readonly DmmItemListClient _client;
        private readonly TimeSpan _requestDelay;

        public DmmMetadataResolveService(DmmItemListClient client, TimeSpan? requestDelay = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requestDelay = requestDelay ?? TimeSpan.FromMilliseconds(250);
        }

        public Task<DmmResolveResult> ResolveAsync(
            string movieName,
            CancellationToken cancellationToken = default)
        {
            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(movieName);
            if (!extracted.HasProductCode)
            {
                return Task.FromResult(DmmResolveResult.Skip(
                    DmmResolveOutcome.NoProductCode,
                    "品番なし",
                    DmmInitialKeyword.FromMovieName(movieName)));
            }

            return ResolveByProductCodeAsync(
                extracted.ProductCode,
                extracted.SpaceForm,
                extracted.CidCandidates,
                cancellationToken);
        }

        public async Task<DmmKeywordSearchResult> SearchKeywordAsync(
            string keyword,
            CancellationToken cancellationToken = default,
            int hits = 10,
            int offset = 1)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return DmmKeywordSearchResult.Empty(keyword);
            }

            DmmSearchResult result = await _client
                .SearchByKeywordSiteAsync(keyword, cancellationToken, hits, offset)
                .ConfigureAwait(false);

            return MapKeywordSearchResult(result, keyword);
        }

        /// <summary>
        /// 手動検索ウィンドウ向けの1ページ取得（CID videoa/videoc/dvd + キーワード、同一 offset/hits）。
        /// 既定 hits=30 は SearchManualAsync のキーワード件数と揃える。
        /// </summary>
        public async Task<DmmKeywordSearchResult> SearchPageAsync(
            string keyword,
            int offset = 1,
            int hits = 30,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return DmmKeywordSearchResult.Empty(keyword);
            }

            string trimmed = keyword.Trim();
            int pageHits = Math.Clamp(hits, 1, 100);
            int pageOffset = Math.Max(1, offset);
            var merged = new List<DmmCandidateEntry>();
            bool mayHaveMore = false;
            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromSearchInput(trimmed);

            if (extracted.HasProductCode && extracted.CidCandidates is { Count: > 0 })
            {
                foreach (string cid in extracted.CidCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DmmSearchResult digital = await _client
                        .SearchByCidDigitalAsync(cid, cancellationToken, pageHits, pageOffset)
                        .ConfigureAwait(false);
                    if (digital.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    if (digital.Status == DmmSearchStatus.HttpError && merged.Count == 0)
                    {
                        // 他経路を試す
                    }
                    else if (digital.Status != DmmSearchStatus.HttpError)
                    {
                        AppendSearchHits(merged, digital);
                        mayHaveMore |= (digital.Items?.Count ?? 0) >= pageHits;
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);

                    DmmSearchResult amateur = await _client
                        .SearchByCidAmateurAsync(cid, cancellationToken, pageHits, pageOffset)
                        .ConfigureAwait(false);
                    if (amateur.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    if (amateur.Status != DmmSearchStatus.HttpError)
                    {
                        AppendSearchHits(merged, amateur);
                        mayHaveMore |= (amateur.Items?.Count ?? 0) >= pageHits;
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);

                    DmmSearchResult dvd = await _client
                        .SearchByCidDvdAsync(cid, cancellationToken, pageHits, pageOffset)
                        .ConfigureAwait(false);
                    if (dvd.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    if (dvd.Status != DmmSearchStatus.HttpError)
                    {
                        AppendSearchHits(merged, dvd);
                        mayHaveMore |= (dvd.Items?.Count ?? 0) >= pageHits;
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            DmmSearchResult keywordResult = await _client
                .SearchByKeywordSiteAsync(trimmed, cancellationToken, pageHits, pageOffset)
                .ConfigureAwait(false);
            if (keywordResult.Status == DmmSearchStatus.NotConfigured)
            {
                return DmmKeywordSearchResult.NotConfigured(trimmed);
            }

            if (keywordResult.Status == DmmSearchStatus.HttpError && merged.Count == 0)
            {
                return DmmKeywordSearchResult.HttpError(trimmed, keywordResult.ErrorMessage);
            }

            if (keywordResult.Status != DmmSearchStatus.HttpError)
            {
                AppendSearchHits(merged, keywordResult);
                mayHaveMore |= (keywordResult.Items?.Count ?? 0) >= pageHits;
            }

            return DmmKeywordSearchResult.FromItems(trimmed, merged, mayHaveMore);
        }

        /// <summary>
        /// 手動検索。CID＋入力語キーワードを試し、品番一致ジャケ（品番なし時は任意ジャケ）で打ち切り。
        /// 未確定ならスペース表記・5桁ゼロ埋めキーワードを最大1回ずつ追加する。
        /// </summary>
        public async Task<DmmKeywordSearchResult> SearchManualAsync(
            string keyword,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return DmmKeywordSearchResult.Empty(keyword);
            }

            string trimmed = keyword.Trim();
            var merged = new List<DmmCandidateEntry>();
            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromSearchInput(trimmed);

            DmmKeywordSearchResult phase1 = await RunManualFirstPhaseAsync(
                    trimmed,
                    extracted,
                    merged,
                    cancellationToken)
                .ConfigureAwait(false);
            if (phase1 != null)
            {
                return phase1;
            }

            // 一致ジャケなし → スペース表記を1回だけ追加
            string spaceForm = extracted.SpaceForm;
            if (!string.IsNullOrWhiteSpace(spaceForm)
                && !string.Equals(spaceForm, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                DmmKeywordSearchResult spaceResult = await SearchKeywordAsync(spaceForm, cancellationToken, hits: 30)
                    .ConfigureAwait(false);
                if (!spaceResult.IsConfigured)
                {
                    return spaceResult;
                }

                if (!string.IsNullOrWhiteSpace(spaceResult.ErrorMessage) && merged.Count == 0)
                {
                    return spaceResult;
                }

                MergeCandidates(merged, spaceResult.Candidates);
                if (ShouldStopManualSearch(merged, extracted))
                {
                    return DmmKeywordSearchResult.FromItems(trimmed, merged);
                }
            }

            // まだ一致ジャケなし → maker+5桁ゼロ埋めを1回
            await TryAppendPadded5KeywordAsync(trimmed, extracted, merged, cancellationToken)
                .ConfigureAwait(false);

            if (merged.Count == 0)
            {
                return DmmKeywordSearchResult.Empty(trimmed);
            }

            return DmmKeywordSearchResult.FromItems(trimmed, merged);
        }

        /// <summary>
        /// 第1段（CID＋入力語KW）。未設定/HTTPエラーのみ即返す。打ち切り条件を満たせば結果を返す。
        /// 通常の継続時は null。
        /// </summary>
        private async Task<DmmKeywordSearchResult> RunManualFirstPhaseAsync(
            string trimmed,
            DmmCidNormalizer.ExtractResult extracted,
            List<DmmCandidateEntry> merged,
            CancellationToken cancellationToken)
        {
            if (extracted.HasProductCode && extracted.CidCandidates is { Count: > 0 })
            {
                foreach (string cid in extracted.CidCandidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    DmmSearchResult digital = await _client
                        .SearchByCidDigitalAsync(cid, cancellationToken)
                        .ConfigureAwait(false);
                    if (digital.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    if (digital.Status == DmmSearchStatus.HttpError && merged.Count == 0)
                    {
                        // CID エラーでも他経路を試すため一旦継続（最終的に0件なら後で Empty）
                    }
                    else
                    {
                        AppendSearchHits(merged, digital);
                    }

                    if (ShouldStopManualSearch(merged, extracted))
                    {
                        return DmmKeywordSearchResult.FromItems(trimmed, merged);
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);

                    DmmSearchResult amateur = await _client
                        .SearchByCidAmateurAsync(cid, cancellationToken)
                        .ConfigureAwait(false);
                    if (amateur.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    if (amateur.Status != DmmSearchStatus.HttpError)
                    {
                        AppendSearchHits(merged, amateur);
                    }

                    if (ShouldStopManualSearch(merged, extracted))
                    {
                        return DmmKeywordSearchResult.FromItems(trimmed, merged);
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);

                    DmmSearchResult dvd = await _client
                        .SearchByCidDvdAsync(cid, cancellationToken)
                        .ConfigureAwait(false);
                    if (dvd.Status == DmmSearchStatus.NotConfigured)
                    {
                        return DmmKeywordSearchResult.NotConfigured(trimmed);
                    }

                    AppendSearchHits(merged, dvd);
                    if (ShouldStopManualSearch(merged, extracted))
                    {
                        return DmmKeywordSearchResult.FromItems(trimmed, merged);
                    }

                    await DelayAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            DmmKeywordSearchResult keywordResult = await SearchKeywordAsync(trimmed, cancellationToken, hits: 30)
                .ConfigureAwait(false);
            if (!keywordResult.IsConfigured)
            {
                return keywordResult;
            }

            if (!string.IsNullOrWhiteSpace(keywordResult.ErrorMessage) && merged.Count == 0)
            {
                return keywordResult;
            }

            MergeCandidates(merged, keywordResult.Candidates);
            if (ShouldStopManualSearch(merged, extracted))
            {
                return DmmKeywordSearchResult.FromItems(trimmed, merged);
            }

            return null;
        }

        private async Task<DmmResolveResult> ResolveByProductCodeAsync(
            string productCode,
            string spaceForm,
            IReadOnlyList<string> cidCandidates,
            CancellationToken cancellationToken)
        {
            bool sawHttpError = false;
            string lastHttpError = null;
            List<DmmCandidateEntry> candidates = [];

            foreach (string cid in cidCandidates ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();

                DmmSearchResult digital = await _client
                    .SearchByCidDigitalAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult digitalResult = InterpretWithJacketPolicy(
                    digital,
                    productCode,
                    candidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (digitalResult != null)
                {
                    return digitalResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult amateur = await _client
                    .SearchByCidAmateurAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult amateurResult = InterpretWithJacketPolicy(
                    amateur,
                    productCode,
                    candidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (amateurResult != null)
                {
                    return amateurResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult dvd = await _client
                    .SearchByCidDvdAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult dvdResult = InterpretWithJacketPolicy(
                    dvd,
                    productCode,
                    candidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (dvdResult != null)
                {
                    return dvdResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }

            // 第1段のキーワード（ハイフン品番）
            DmmSearchResult hyphenKeyword = await _client
                .SearchByKeywordSiteAsync(productCode, cancellationToken)
                .ConfigureAwait(false);
            DmmResolveResult hyphenResult = InterpretWithJacketPolicy(
                hyphenKeyword,
                productCode,
                candidates,
                ref sawHttpError,
                ref lastHttpError);
            if (hyphenResult != null)
            {
                return hyphenResult;
            }

            // 一致ジャケなし → スペース表記を1回だけ
            if (!string.IsNullOrWhiteSpace(spaceForm)
                && !string.Equals(spaceForm, productCode, StringComparison.OrdinalIgnoreCase))
            {
                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult spaceKeyword = await _client
                    .SearchByKeywordSiteAsync(spaceForm, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult spaceResult = InterpretWithJacketPolicy(
                    spaceKeyword,
                    productCode,
                    candidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (spaceResult != null)
                {
                    return spaceResult;
                }
            }

            // まだ一致ジャケなし → maker+5桁ゼロ埋めキーワードを1回
            string padded5 = DmmProductCodeMatcher.BuildPadded5Keyword(productCode);
            if (!string.IsNullOrWhiteSpace(padded5)
                && !string.Equals(padded5, productCode, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(padded5, spaceForm, StringComparison.OrdinalIgnoreCase))
            {
                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult paddedKeyword = await _client
                    .SearchByKeywordSiteAsync(padded5, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult paddedResult = InterpretWithJacketPolicy(
                    paddedKeyword,
                    productCode,
                    candidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (paddedResult != null)
                {
                    return paddedResult;
                }
            }

            // 一致ジャケなしのまま終了（候補は残す）
            if (candidates.Count > 0)
            {
                return DmmResolveResult.Ambiguous(
                    candidates,
                    productCode,
                    productCode);
            }

            if (sawHttpError)
            {
                return DmmResolveResult.Skip(DmmResolveOutcome.HttpError, lastHttpError);
            }

            return DmmResolveResult.Skip(DmmResolveOutcome.NotFound, "未ヒット", productCode);
        }

        private async Task TryAppendPadded5KeywordAsync(
            string trimmed,
            DmmCidNormalizer.ExtractResult extracted,
            List<DmmCandidateEntry> merged,
            CancellationToken cancellationToken)
        {
            if (!extracted.HasProductCode || ShouldStopManualSearch(merged, extracted))
            {
                return;
            }

            string padded5 = DmmProductCodeMatcher.BuildPadded5Keyword(extracted.ProductCode);
            if (string.IsNullOrWhiteSpace(padded5)
                || string.Equals(padded5, trimmed, StringComparison.OrdinalIgnoreCase)
                || string.Equals(padded5, extracted.SpaceForm, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DelayAsync(cancellationToken).ConfigureAwait(false);

            DmmKeywordSearchResult paddedResult = await SearchKeywordAsync(padded5, cancellationToken, hits: 30)
                .ConfigureAwait(false);
            if (!paddedResult.IsConfigured || !string.IsNullOrWhiteSpace(paddedResult.ErrorMessage))
            {
                return;
            }

            MergeCandidates(merged, paddedResult.Candidates);
        }

        private static bool ShouldStopManualSearch(
            List<DmmCandidateEntry> merged,
            DmmCidNormalizer.ExtractResult extracted)
        {
            if (extracted.HasProductCode)
            {
                return DmmJacketHitEvaluator.HasProductMatchingUsableJacket(merged, extracted.ProductCode);
            }

            return DmmJacketHitEvaluator.HasAnyUsableJacket(merged);
        }

        private static DmmResolveResult InterpretWithJacketPolicy(
            DmmSearchResult result,
            string productCode,
            List<DmmCandidateEntry> candidates,
            ref bool sawHttpError,
            ref string lastHttpError)
        {
            switch (result.Status)
            {
                case DmmSearchStatus.NotConfigured:
                    return DmmResolveResult.Skip(DmmResolveOutcome.NotConfigured, "API未設定");
                case DmmSearchStatus.HttpError:
                    sawHttpError = true;
                    lastHttpError = result.ErrorMessage;
                    return null;
                case DmmSearchStatus.OneHit:
                case DmmSearchStatus.MultipleHits:
                    AppendSearchHits(candidates, result);
                    return DmmJacketHitEvaluator.TryConclude(candidates, productCode);
                default:
                    return null;
            }
        }

        private static void AppendSearchHits(List<DmmCandidateEntry> target, DmmSearchResult result)
        {
            if (result == null)
            {
                return;
            }

            switch (result.Status)
            {
                case DmmSearchStatus.OneHit when result.Item != null:
                    AddSingleCandidate(target, result.Item, result.FloorLabel);
                    break;
                case DmmSearchStatus.MultipleHits:
                    AddCandidates(target, result);
                    break;
            }
        }

        private static void AddSingleCandidate(
            List<DmmCandidateEntry> target,
            DmmItemDto item,
            string floorLabel)
        {
            if (item == null)
            {
                return;
            }

            if (target.Any(existing =>
                    string.Equals(existing.Item?.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.FloorLabel, floorLabel, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Add(new DmmCandidateEntry
            {
                Item = item,
                FloorLabel = floorLabel ?? string.Empty,
            });
        }

        private static void MergeCandidates(
            List<DmmCandidateEntry> target,
            IReadOnlyList<DmmCandidateEntry> source)
        {
            if (source == null || source.Count == 0)
            {
                return;
            }

            foreach (DmmCandidateEntry entry in source)
            {
                AddSingleCandidate(target, entry.Item, entry.FloorLabel);
            }
        }

        private static void AddCandidates(List<DmmCandidateEntry> target, DmmSearchResult result)
        {
            if (result.Items == null)
            {
                return;
            }

            foreach (DmmItemDto item in result.Items)
            {
                if (item == null)
                {
                    continue;
                }

                if (target.Any(existing =>
                        string.Equals(existing.Item?.ContentId, item.ContentId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.FloorLabel, result.FloorLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                target.Add(new DmmCandidateEntry
                {
                    Item = item,
                    FloorLabel = result.FloorLabel,
                });
            }
        }

        private static DmmKeywordSearchResult MapKeywordSearchResult(DmmSearchResult result, string keyword)
        {
            return result.Status switch
            {
                DmmSearchStatus.NotConfigured => DmmKeywordSearchResult.NotConfigured(keyword),
                DmmSearchStatus.HttpError => DmmKeywordSearchResult.HttpError(keyword, result.ErrorMessage),
                DmmSearchStatus.ZeroHits => DmmKeywordSearchResult.Empty(keyword),
                _ => DmmKeywordSearchResult.FromItems(
                    keyword,
                    result.Items?.Select(item => new DmmCandidateEntry
                    {
                        Item = item,
                        FloorLabel = result.FloorLabel ?? "keyword",
                    }).ToList() ?? []),
            };
        }

        private async Task DelayAsync(CancellationToken cancellationToken)
        {
            if (_requestDelay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(_requestDelay, cancellationToken).ConfigureAwait(false);
        }
    }

    internal sealed class DmmKeywordSearchResult
    {
        public string Keyword { get; init; }
        public bool IsConfigured { get; init; } = true;
        public string ErrorMessage { get; init; }
        public IReadOnlyList<DmmCandidateEntry> Candidates { get; init; } = [];
        public bool MayHaveMore { get; init; }

        public static DmmKeywordSearchResult Empty(string keyword) =>
            new() { Keyword = keyword ?? string.Empty };

        public static DmmKeywordSearchResult NotConfigured(string keyword) =>
            new() { Keyword = keyword ?? string.Empty, IsConfigured = false };

        public static DmmKeywordSearchResult HttpError(string keyword, string message) =>
            new() { Keyword = keyword ?? string.Empty, ErrorMessage = message };

        public static DmmKeywordSearchResult FromItems(
            string keyword,
            IReadOnlyList<DmmCandidateEntry> candidates,
            bool mayHaveMore = false) =>
            new()
            {
                Keyword = keyword ?? string.Empty,
                Candidates = candidates ?? [],
                MayHaveMore = mayHaveMore,
            };
    }
}
