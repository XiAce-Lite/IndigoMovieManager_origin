namespace IndigoMovieManager.Services.Dmm
{
    internal static class DmmMetadataEligibility
    {
        public static bool NeedsFetch(string title, string comment1) =>
            string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(comment1);
    }
}
