using System.IO;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// マニュアルプレビューからのブックマーク用サムネ命名。
    /// </summary>
    internal static class BookmarkCaptureNaming
    {
        public static string BuildThumbBody(
            string movieBody,
            int positionSeconds,
            double fps,
            DateTime timestamp)
        {
            string body = movieBody ?? "";
            int targetFrame = positionSeconds * (int)fps;
            string timeLabel = timestamp.ToString("HH-mm-ss");
            return $"{body}[({targetFrame}){timeLabel}]";
        }

        public static string BuildThumbFileName(string thumbBody) =>
            $"{thumbBody ?? ""}.jpg";

        public static string BuildThumbFilePath(string bookmarkFolder, string thumbBody) =>
            Path.Combine(bookmarkFolder ?? "", BuildThumbFileName(thumbBody));

        public static string ResolveFolderOrDefault(string configuredFolder, string dbName) =>
            BookmarkRecordMapper.ResolveBookmarkFolder(configuredFolder, dbName);
    }
}
