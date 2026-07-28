using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace IndigoMovieManager.Services.Dmm
{
    internal enum DmmSearchStatus
    {
        NotConfigured,
        HttpError,
        ZeroHits,
        OneHit,
        MultipleHits,
    }

    internal sealed class DmmSearchResult
    {
        public DmmSearchStatus Status { get; init; }
        public DmmItemDto Item { get; init; }
        public IReadOnlyList<DmmItemDto> Items { get; init; } = [];
        public int HitCount { get; init; }
        public string ErrorMessage { get; init; }
        public string FloorLabel { get; init; }

        public static DmmSearchResult NotConfigured() =>
            new() { Status = DmmSearchStatus.NotConfigured };

        public static DmmSearchResult HttpError(string message) =>
            new() { Status = DmmSearchStatus.HttpError, ErrorMessage = message };

        public static DmmSearchResult FromItems(IReadOnlyList<DmmItemDto> items, string floorLabel)
        {
            IReadOnlyList<DmmItemDto> normalized = items ?? [];
            int count = normalized.Count;
            if (count == 0)
            {
                return new DmmSearchResult
                {
                    Status = DmmSearchStatus.ZeroHits,
                    HitCount = 0,
                    FloorLabel = floorLabel,
                    Items = normalized,
                };
            }

            if (count == 1)
            {
                return new DmmSearchResult
                {
                    Status = DmmSearchStatus.OneHit,
                    HitCount = 1,
                    Item = normalized[0],
                    Items = normalized,
                    FloorLabel = floorLabel,
                };
            }

            return new DmmSearchResult
            {
                Status = DmmSearchStatus.MultipleHits,
                HitCount = count,
                Items = normalized,
                FloorLabel = floorLabel,
            };
        }
    }

    internal sealed class DmmItemListClient
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly DmmApiOptions _options;

        public DmmItemListClient(DmmApiOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IndigoMovieManager");
            return client;
        }

        public Task<DmmSearchResult> SearchByCidDigitalAsync(
            string cid,
            CancellationToken cancellationToken = default,
            int hits = 10,
            int offset = 1) =>
            SearchByCidAsync(cid, "digital", "videoa", cancellationToken, hits, offset);

        public Task<DmmSearchResult> SearchByCidDvdAsync(
            string cid,
            CancellationToken cancellationToken = default,
            int hits = 10,
            int offset = 1) =>
            SearchByCidAsync(cid, "mono", "dvd", cancellationToken, hits, offset);

        public async Task<DmmSearchResult> SearchByKeywordSiteAsync(
            string keyword,
            CancellationToken cancellationToken = default,
            int hits = 10,
            int offset = 1)
        {
            if (!_options.IsConfigured)
            {
                return DmmSearchResult.NotConfigured();
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                return DmmSearchResult.FromItems([], "keyword");
            }

            string query = BuildKeywordSearchQuery(
                _options.ApiId,
                _options.AffiliateId,
                keyword.Trim(),
                hits,
                offset);

            return await GetAsync(query, "keyword", cancellationToken).ConfigureAwait(false);
        }

        private async Task<DmmSearchResult> SearchByCidAsync(
            string cid,
            string service,
            string floor,
            CancellationToken cancellationToken,
            int hits = 10,
            int offset = 1)
        {
            if (!_options.IsConfigured)
            {
                return DmmSearchResult.NotConfigured();
            }

            if (string.IsNullOrWhiteSpace(cid))
            {
                return DmmSearchResult.FromItems([], $"{service}/{floor}");
            }

            string query = BuildCidSearchQuery(
                _options.ApiId,
                _options.AffiliateId,
                cid.Trim(),
                service,
                floor,
                hits,
                offset);

            return await GetAsync(query, $"{service}/{floor}", cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>単体テスト向け。キーワード検索クエリを組み立てる。</summary>
        internal static string BuildKeywordSearchQuery(
            string apiId,
            string affiliateId,
            string keyword,
            int hits = 10,
            int offset = 1)
        {
            int clampedHits = Math.Clamp(hits, 1, 100);
            int clampedOffset = Math.Max(1, offset);
            return BuildQuery(
                ("api_id", apiId),
                ("affiliate_id", affiliateId),
                ("site", "FANZA"),
                ("hits", clampedHits.ToString()),
                ("offset", clampedOffset.ToString()),
                ("keyword", keyword ?? string.Empty),
                ("output", "json"));
        }

        /// <summary>単体テスト向け。CID 検索クエリを組み立てる。</summary>
        internal static string BuildCidSearchQuery(
            string apiId,
            string affiliateId,
            string cid,
            string service,
            string floor,
            int hits = 10,
            int offset = 1)
        {
            int clampedHits = Math.Clamp(hits, 1, 100);
            int clampedOffset = Math.Max(1, offset);
            return BuildQuery(
                ("api_id", apiId),
                ("affiliate_id", affiliateId),
                ("site", "FANZA"),
                ("service", service ?? string.Empty),
                ("floor", floor ?? string.Empty),
                ("hits", clampedHits.ToString()),
                ("offset", clampedOffset.ToString()),
                ("cid", cid ?? string.Empty),
                ("output", "json"));
        }

        private async Task<DmmSearchResult> GetAsync(
            string query,
            string floorLabel,
            CancellationToken cancellationToken)
        {
            string url = "https://api.dmm.com/affiliate/v3/ItemList?" + query;
            try
            {
                using HttpResponseMessage response =
                    await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return DmmSearchResult.HttpError(
                        $"HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
                }

                DmmItemListResponse parsed =
                    JsonSerializer.Deserialize<DmmItemListResponse>(body, JsonOptions);
                IReadOnlyList<DmmItemDto> items = parsed?.Result?.Items ?? [];
                return DmmSearchResult.FromItems(items, floorLabel);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return DmmSearchResult.HttpError(ex.Message);
            }
        }

        private static string BuildQuery(params (string Key, string Value)[] pairs)
        {
            var sb = new StringBuilder();
            foreach ((string key, string value) in pairs)
            {
                if (sb.Length > 0)
                {
                    sb.Append('&');
                }

                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value ?? ""));
            }

            return sb.ToString();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text[..max] + "…";
        }
    }
}
