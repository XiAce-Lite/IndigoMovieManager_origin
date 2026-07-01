namespace IndigoMovieManager.Thumbnail
{
    /// <summary>
    /// ステータスバー進捗の詳細行（フルパス + バックエンド + 動画コーデック）。
    /// </summary>
    internal static class ThumbnailProgressDetailFormatter
    {
        public static string Format(string fullPath, ThumbnailCreateResult result, string videoCodec)
        {
            string path = fullPath?.Trim() ?? string.Empty;
            string backend = FormatBackendLabel(result);
            string codec = string.IsNullOrWhiteSpace(videoCodec) ? string.Empty : videoCodec.Trim();

            if (string.IsNullOrEmpty(backend) && string.IsNullOrEmpty(codec))
            {
                return path;
            }

            if (string.IsNullOrEmpty(backend))
            {
                return $"{path}  |  {codec}";
            }

            if (string.IsNullOrEmpty(codec))
            {
                return $"{path}  |  {backend}";
            }

            return $"{path}  |  {backend}  |  {codec}";
        }

        private static string FormatBackendLabel(ThumbnailCreateResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Backend))
            {
                return string.Empty;
            }

            if (!result.Backend.Equals("FFmpeg", StringComparison.OrdinalIgnoreCase))
            {
                return result.Backend;
            }

            string decoder = result.Decoder?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(decoder)
                || decoder.Equals("software", StringComparison.OrdinalIgnoreCase))
            {
                return "FFmpeg (software)";
            }

            return $"FFmpeg ({decoder})";
        }
    }
}
