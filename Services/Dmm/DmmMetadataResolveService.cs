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
                return Task.FromResult(DmmResolveResult.Skip(DmmResolveOutcome.NoProductCode, "品番なし"));
            }

            return ResolveByProductCodeAsync(extracted.ProductCode, extracted.CidCandidates, cancellationToken);
        }

        public async Task<DmmKeywordSearchResult> SearchKeywordAsync(
            string keyword,
            CancellationToken cancellationToken = default,
            int hits = 10)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return DmmKeywordSearchResult.Empty(keyword);
            }

            DmmSearchResult result = await _client
                .SearchByKeywordSiteAsync(keyword, cancellationToken, hits)
                .ConfigureAwait(false);

            return MapKeywordSearchResult(result, keyword);
        }

        /// <summary>
        /// 手動検索向け。入力語から CID 直検索とキーワード検索の両方を行い、候補をマージする。
        /// キーワード検索は最大 30 件取得する。
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

            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(trimmed);
            if (extracted.HasProductCode && extracted.CidCandidates is { Count: > 0 })
            {
                IReadOnlyList<DmmCandidateEntry> cidHits = await CollectAllCidHitsAsync(
                        extracted.CidCandidates,
                        cancellationToken)
                    .ConfigureAwait(false);
                MergeCandidates(merged, cidHits);
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

            if (merged.Count == 0 && !string.IsNullOrWhiteSpace(keywordResult.ErrorMessage))
            {
                return DmmKeywordSearchResult.HttpError(trimmed, keywordResult.ErrorMessage);
            }

            return DmmKeywordSearchResult.FromItems(trimmed, merged);
        }

        private async Task<DmmResolveResult> ResolveByProductCodeAsync(
            string productCode,
            IReadOnlyList<string> cidCandidates,
            CancellationToken cancellationToken)
        {
            bool sawHttpError = false;
            string lastHttpError = null;
            List<DmmCandidateEntry> ambiguousCandidates = [];

            foreach (string cid in cidCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DmmSearchResult digital = await _client
                    .SearchByCidDigitalAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult digitalResult = Interpret(
                    digital,
                    productCode,
                    ambiguousCandidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (digitalResult != null)
                {
                    return digitalResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult dvd = await _client
                    .SearchByCidDvdAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult dvdResult = Interpret(
                    dvd,
                    productCode,
                    ambiguousCandidates,
                    ref sawHttpError,
                    ref lastHttpError);
                if (dvdResult != null)
                {
                    return dvdResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }

            DmmSearchResult keyword = await _client
                .SearchByKeywordSiteAsync(productCode, cancellationToken)
                .ConfigureAwait(false);
            DmmResolveResult keywordResult = Interpret(
                keyword,
                productCode,
                ambiguousCandidates,
                ref sawHttpError,
                ref lastHttpError);
            if (keywordResult != null)
            {
                return keywordResult;
            }

            if (ambiguousCandidates.Count > 0)
            {
                return DmmResolveResult.Ambiguous(
                    ambiguousCandidates,
                    productCode,
                    DmmInitialKeyword.FromMovieName(productCode));
            }

            if (sawHttpError)
            {
                return DmmResolveResult.Skip(DmmResolveOutcome.HttpError, lastHttpError);
            }

            return DmmResolveResult.Skip(DmmResolveOutcome.NotFound, "未ヒット");
        }

        private async Task<IReadOnlyList<DmmCandidateEntry>> CollectAllCidHitsAsync(
            IReadOnlyList<string> cidCandidates,
            CancellationToken cancellationToken)
        {
            var collected = new List<DmmCandidateEntry>();

            foreach (string cid in cidCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DmmSearchResult digital = await _client
                    .SearchByCidDigitalAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                AppendSearchHits(collected, digital);

                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult dvd = await _client
                    .SearchByCidDvdAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                AppendSearchHits(collected, dvd);

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }

            return collected;
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

        private static DmmResolveResult Interpret(
            DmmSearchResult result,
            string productCode,
            List<DmmCandidateEntry> ambiguousCandidates,
            ref bool sawHttpError,
            ref string lastHttpError)
        {
            switch (result.Status)
            {
                case DmmSearchStatus.NotConfigured:
                    return DmmResolveResult.Skip(DmmResolveOutcome.NotConfigured, "API未設定");
                case DmmSearchStatus.OneHit:
                    return DmmResolveResult.Applied(result.Item, productCode);
                case DmmSearchStatus.MultipleHits:
                    AddCandidates(ambiguousCandidates, result);
                    return null;
                case DmmSearchStatus.HttpError:
                    sawHttpError = true;
                    lastHttpError = result.ErrorMessage;
                    return null;
                default:
                    return null;
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

        public static DmmKeywordSearchResult Empty(string keyword) =>
            new() { Keyword = keyword ?? string.Empty };

        public static DmmKeywordSearchResult NotConfigured(string keyword) =>
            new() { Keyword = keyword ?? string.Empty, IsConfigured = false };

        public static DmmKeywordSearchResult HttpError(string keyword, string message) =>
            new() { Keyword = keyword ?? string.Empty, ErrorMessage = message };

        public static DmmKeywordSearchResult FromItems(string keyword, IReadOnlyList<DmmCandidateEntry> candidates) =>
            new() { Keyword = keyword ?? string.Empty, Candidates = candidates ?? [] };
    }
}

