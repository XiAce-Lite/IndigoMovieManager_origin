namespace IndigoMovieManager.Services.Dmm
{
    internal static class DmmJacketUrls
    {
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
    }
}
