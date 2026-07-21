using System.Text.Json.Serialization;

namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmItemListResponse
    {
        [JsonPropertyName("result")]
        public DmmItemListResult Result { get; set; }
    }

    internal sealed class DmmItemListResult
    {
        [JsonPropertyName("status")]
        public object Status { get; set; }

        [JsonPropertyName("result_count")]
        public int ResultCount { get; set; }

        [JsonPropertyName("total_count")]
        public object TotalCount { get; set; }

        [JsonPropertyName("items")]
        public List<DmmItemDto> Items { get; set; }
    }

    internal sealed class DmmItemDto
    {
        [JsonPropertyName("content_id")]
        public string ContentId { get; set; }

        [JsonPropertyName("product_id")]
        public string ProductId { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("URL")]
        public string Url { get; set; }

        [JsonPropertyName("affiliateURL")]
        public string AffiliateUrl { get; set; }

        [JsonPropertyName("imageURL")]
        public DmmImageUrlDto ImageUrl { get; set; }

        [JsonPropertyName("iteminfo")]
        public DmmItemInfo ItemInfo { get; set; }
    }

    internal sealed class DmmImageUrlDto
    {
        [JsonPropertyName("list")]
        public string List { get; set; }

        [JsonPropertyName("small")]
        public string Small { get; set; }

        [JsonPropertyName("large")]
        public string Large { get; set; }
    }

    internal sealed class DmmItemInfo
    {
        [JsonPropertyName("actress")]
        public List<DmmNamedEntity> Actress { get; set; }

        [JsonPropertyName("genre")]
        public List<DmmNamedEntity> Genre { get; set; }

        [JsonPropertyName("maker")]
        public List<DmmNamedEntity> Maker { get; set; }

        [JsonPropertyName("label")]
        public List<DmmNamedEntity> Label { get; set; }

        [JsonPropertyName("series")]
        public List<DmmNamedEntity> Series { get; set; }
    }

    internal sealed class DmmNamedEntity
    {
        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("ruby")]
        public string Ruby { get; set; }
    }
}
