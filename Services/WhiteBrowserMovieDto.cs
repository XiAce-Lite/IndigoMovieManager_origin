using System.Text.Json.Serialization;

namespace IndigoMovieManager.Services
{
    /// <summary>WhiteBrowser スキンが期待する mv オブジェクト（wblib / onCreateThum 引数）。</summary>
    internal sealed class WhiteBrowserMovieDto
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("thum")]
        public string Thum { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; }

        [JsonPropertyName("ext")]
        public string Ext { get; init; }

        [JsonPropertyName("exist")]
        public bool Exist { get; init; }

        [JsonPropertyName("select")]
        public int Select { get; init; }

        [JsonPropertyName("score")]
        public long Score { get; init; }

        [JsonPropertyName("fileDate")]
        public string FileDate { get; init; }

        [JsonPropertyName("size")]
        public string Size { get; init; }

        [JsonPropertyName("len")]
        public string Len { get; init; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; init; }

        [JsonPropertyName("drive")]
        public string Drive { get; init; }

        [JsonPropertyName("dir")]
        public string Dir { get; init; }

        [JsonPropertyName("container")]
        public string Container { get; init; }

        [JsonPropertyName("video")]
        public string Video { get; init; }

        [JsonPropertyName("audio")]
        public string Audio { get; init; }

        [JsonPropertyName("comments")]
        public string[] Comments { get; init; }
    }
}
