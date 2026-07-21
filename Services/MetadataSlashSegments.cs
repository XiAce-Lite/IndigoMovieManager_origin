namespace IndigoMovieManager.Services
{
    /// <summary>
    /// DMM 等で <c> / </c> 結合されたメタデータ文字列を検索用セグメントに分割する。
    /// </summary>
    internal static class MetadataSlashSegments
    {
        public const string Separator = " / ";

        public static IReadOnlyList<string> Split(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            string[] parts = value.Split(
                [Separator],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                return [];
            }

            var segments = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    segments.Add(part);
                }
            }

            return segments;
        }
    }
}
