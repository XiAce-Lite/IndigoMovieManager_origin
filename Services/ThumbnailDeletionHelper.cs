using System.IO;

namespace IndigoMovieManager.Services
{
    internal static class ThumbnailDeletionHelper
    {
        public static void DeleteThumbnailsForRecord(string thumbFolder, string dbName, string movieBody, string hash)
        {
            string resolvedFolder = string.IsNullOrEmpty(thumbFolder)
                ? Path.Combine(Directory.GetCurrentDirectory(), "Thumb", dbName)
                : thumbFolder;

            if (!Path.Exists(resolvedFolder))
            {
                return;
            }

            DirectoryInfo di = new(resolvedFolder);
            EnumerationOptions enumOption = new() { RecurseSubdirectories = true };
            IEnumerable<FileInfo> ssFiles = di.EnumerateFiles($"*{movieBody}.#{hash}*.jpg", enumOption);
            foreach (FileInfo item in ssFiles)
            {
                item.Delete();
            }
        }
    }
}
