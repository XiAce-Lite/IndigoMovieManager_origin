using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// GitHub Releases の最新版を確認する（通知のみ。自動適用はしない）。
    /// </summary>
    internal static class UpdateCheckService
    {
        internal const string Owner = "XiAce-Lite";
        internal const string Repo = "IndigoMovieManager_origin";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IndigoMovieManager");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public sealed record ReleaseInfo(Version Version, string TagName, string HtmlUrl);

        public static Version GetCurrentVersion()
        {
            string text = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "0.0.0.0";
            return Version.TryParse(text, out Version version)
                ? version
                : new Version(0, 0, 0, 0);
        }

        public static bool TryParseTagVersion(string tagName, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            string text = tagName.Trim();
            if (text.StartsWith('v') || text.StartsWith('V'))
            {
                text = text[1..];
            }

            return Version.TryParse(text, out version);
        }

        /// <summary>
        /// 現在より新しい latest Release があれば返す。失敗・同版以下は null。
        /// </summary>
        public static async Task<ReleaseInfo> TryGetNewerReleaseAsync(
            Version currentVersion,
            CancellationToken cancellationToken = default)
        {
            if (currentVersion == null)
            {
                return null;
            }

            try
            {
                string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                using HttpResponseMessage response =
                    await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("tag_name", out JsonElement tagElement))
                {
                    return null;
                }

                string tagName = tagElement.GetString();
                if (!TryParseTagVersion(tagName, out Version latest) || latest <= currentVersion)
                {
                    return null;
                }

                string htmlUrl = root.TryGetProperty("html_url", out JsonElement urlElement)
                    ? urlElement.GetString()
                    : null;
                if (string.IsNullOrEmpty(htmlUrl))
                {
                    htmlUrl = $"https://github.com/{Owner}/{Repo}/releases/tag/{tagName}";
                }

                return new ReleaseInfo(latest, tagName, htmlUrl);
            }
            catch
            {
                return null;
            }
        }
    }
}
