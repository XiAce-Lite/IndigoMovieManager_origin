using System.IO;

namespace IndigoMovieManager.Services
{
    internal static class MovieRenameService
    {
        public static void RenameThumbnailFiles(
            string thumbFolder,
            string dbName,
            string checkFileName,
            string newMovieName,
            string hash,
            MovieRecords item)
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
            IEnumerable<FileInfo> ssFiles = di.EnumerateFiles($"*{checkFileName}.#{hash}*.jpg", enumOption);
            foreach (FileInfo thumbFile in ssFiles)
            {
                string oldFilePath = thumbFile.FullName;
                string newFilePath = oldFilePath.Replace(checkFileName, newMovieName, StringComparison.CurrentCultureIgnoreCase);
                if (item.ThumbPathSmall == oldFilePath) { item.ThumbPathSmall = newFilePath; }
                if (item.ThumbPathBig == oldFilePath) { item.ThumbPathBig = newFilePath; }
                if (item.ThumbPathGrid == oldFilePath) { item.ThumbPathGrid = newFilePath; }
                if (item.ThumbPathList == oldFilePath) { item.ThumbPathList = newFilePath; }
                if (item.ThumbPathBig10 == oldFilePath) { item.ThumbPathBig10 = newFilePath; }
                thumbFile.MoveTo(newFilePath, true);
            }
        }

        public static void RenameBookmarkFiles(
            string bookmarkFolder,
            string dbName,
            string checkFileName,
            string newMovieName)
        {
            string resolvedFolder = string.IsNullOrEmpty(bookmarkFolder)
                ? Path.Combine(Directory.GetCurrentDirectory(), "bookmark", dbName)
                : bookmarkFolder;

            if (!Path.Exists(resolvedFolder))
            {
                return;
            }

            DirectoryInfo di = new(resolvedFolder);
            EnumerationOptions enumOption = new() { RecurseSubdirectories = true };
            IEnumerable<FileInfo> ssFiles = di.EnumerateFiles($"*{checkFileName}*.jpg", enumOption);
            foreach (FileInfo bookMarkJpg in ssFiles)
            {
                string dstFile = bookMarkJpg.FullName.Replace(checkFileName, newMovieName, StringComparison.CurrentCultureIgnoreCase);
                File.Move(bookMarkJpg.FullName, dstFile, true);
            }
        }
    }
}
