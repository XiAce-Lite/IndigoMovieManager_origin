namespace IndigoMovieManager.Services
{
    internal sealed class SkinMovieDto
    {
        public long Id { get; init; }
        public string MovieName { get; init; }
        public string MovieBody { get; init; }
        public string Ext { get; init; }
        public string MoviePath { get; init; }
        public string Thumb { get; init; }
        public long Score { get; init; }
        public string FileDate { get; init; }
        public string SizeText { get; init; }
        public string Length { get; init; }
        public string[] Tags { get; init; }
        public bool Exists { get; init; }
        public bool Selected { get; init; }
        public bool Focused { get; init; }
    }
}
