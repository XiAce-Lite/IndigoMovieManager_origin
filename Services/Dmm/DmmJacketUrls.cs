using System.Net.Http;

namespace IndigoMovieManager.Services.Dmm
{
    internal static class DmmJacketUrls
    {
        private static readonly HttpClient Http = CreateClient();

        public static bool IsPlaceholderJacketUri(Uri uri)
        {
            if (uri == null)
            {
                return true;
            }

            return uri.AbsolutePath.Contains("now_printing", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHttpUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static string GetFrontUrl(MovieRecords record) =>
            record != null && IsHttpUrl(record.Comment1) ? record.Comment1.Trim() : null;

        /// <summary>
        /// リダイレクト後 URL を解決し、now_printing 等のプレースホルダなら null。
        /// </summary>
        public static string ResolveUsableJacketUrl(string url)
        {
            if (!IsHttpUrl(url))
            {
                return null;
            }

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Head, url.Trim());
                using HttpResponseMessage response = Http.Send(request);
                Uri finalUri = response.RequestMessage?.RequestUri ?? new Uri(url.Trim(), UriKind.Absolute);

                if (response.IsSuccessStatusCode)
                {
                    return IsPlaceholderJacketUri(finalUri) ? null : finalUri.ToString();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                {
                    using HttpRequestMessage getRequest = new(HttpMethod.Get, url.Trim());
                    getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    using HttpResponseMessage getResponse = Http.Send(getRequest);
                    finalUri = getResponse.RequestMessage?.RequestUri ?? finalUri;
                    if (getResponse.IsSuccessStatusCode && !IsPlaceholderJacketUri(finalUri))
                    {
                        return finalUri.ToString();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("IndigoMovieManager/1.0");
            return client;
        }
    }
}
