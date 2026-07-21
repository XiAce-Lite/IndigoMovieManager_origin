namespace IndigoMovieManager.Services.Dmm
{
    internal sealed class DmmApiOptions
    {
        public string ApiId { get; init; } = "";
        public string AffiliateId { get; init; } = "";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiId) && !string.IsNullOrWhiteSpace(AffiliateId);

        public static DmmApiOptions FromSettings()
        {
            return new DmmApiOptions
            {
                ApiId = Properties.Settings.Default.DmmApiId?.Trim() ?? "",
                AffiliateId = Properties.Settings.Default.DmmAffiliateId?.Trim() ?? "",
            };
        }
    }
}
