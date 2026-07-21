namespace IndigoMovieManager.Services.Dmm
{
    internal enum DmmResolveOutcome
    {
        Applied,
        NoProductCode,
        NotFound,
        Ambiguous,
        HttpError,
        NotConfigured,
    }

    internal sealed class DmmResolveResult
    {
        public DmmResolveOutcome Outcome { get; init; }
        public DmmItemDto Item { get; init; }
        public string ProductCode { get; init; }
        public string Detail { get; init; }

        public static DmmResolveResult Applied(DmmItemDto item, string productCode) =>
            new()
            {
                Outcome = DmmResolveOutcome.Applied,
                Item = item,
                ProductCode = productCode,
            };

        public static DmmResolveResult Skip(DmmResolveOutcome outcome, string detail = null) =>
            new() { Outcome = outcome, Detail = detail };
    }

    internal sealed class DmmMetadataResolveService
    {
        private readonly DmmItemListClient _client;
        private readonly TimeSpan _requestDelay;

        public DmmMetadataResolveService(DmmItemListClient client, TimeSpan? requestDelay = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requestDelay = requestDelay ?? TimeSpan.FromMilliseconds(250);
        }

        public async Task<DmmResolveResult> ResolveAsync(
            string movieName,
            CancellationToken cancellationToken = default)
        {
            DmmCidNormalizer.ExtractResult extracted = DmmCidNormalizer.ExtractFromFileName(movieName);
            if (!extracted.HasProductCode)
            {
                return DmmResolveResult.Skip(DmmResolveOutcome.NoProductCode, "品番なし");
            }

            bool sawHttpError = false;
            string lastHttpError = null;
            bool sawAmbiguous = false;

            foreach (string cid in extracted.CidCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DmmSearchResult digital = await _client
                    .SearchByCidDigitalAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult digitalResult = Interpret(digital, extracted.ProductCode, ref sawHttpError, ref lastHttpError, ref sawAmbiguous);
                if (digitalResult != null)
                {
                    return digitalResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);

                DmmSearchResult dvd = await _client
                    .SearchByCidDvdAsync(cid, cancellationToken)
                    .ConfigureAwait(false);
                DmmResolveResult dvdResult = Interpret(dvd, extracted.ProductCode, ref sawHttpError, ref lastHttpError, ref sawAmbiguous);
                if (dvdResult != null)
                {
                    return dvdResult;
                }

                await DelayAsync(cancellationToken).ConfigureAwait(false);
            }

            DmmSearchResult keyword = await _client
                .SearchByKeywordSiteAsync(extracted.ProductCode, cancellationToken)
                .ConfigureAwait(false);
            DmmResolveResult keywordResult = Interpret(
                keyword,
                extracted.ProductCode,
                ref sawHttpError,
                ref lastHttpError,
                ref sawAmbiguous);
            if (keywordResult != null)
            {
                return keywordResult;
            }

            if (sawAmbiguous)
            {
                return DmmResolveResult.Skip(DmmResolveOutcome.Ambiguous, "複数候補");
            }

            if (sawHttpError)
            {
                return DmmResolveResult.Skip(DmmResolveOutcome.HttpError, lastHttpError);
            }

            return DmmResolveResult.Skip(DmmResolveOutcome.NotFound, "未ヒット");
        }

        private static DmmResolveResult Interpret(
            DmmSearchResult result,
            string productCode,
            ref bool sawHttpError,
            ref string lastHttpError,
            ref bool sawAmbiguous)
        {
            switch (result.Status)
            {
                case DmmSearchStatus.NotConfigured:
                    return DmmResolveResult.Skip(DmmResolveOutcome.NotConfigured, "API未設定");
                case DmmSearchStatus.OneHit:
                    return DmmResolveResult.Applied(result.Item, productCode);
                case DmmSearchStatus.MultipleHits:
                    sawAmbiguous = true;
                    return null;
                case DmmSearchStatus.HttpError:
                    sawHttpError = true;
                    lastHttpError = result.ErrorMessage;
                    return null;
                default:
                    return null;
            }
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
}
