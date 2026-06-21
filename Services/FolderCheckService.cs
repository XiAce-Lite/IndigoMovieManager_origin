using System.Data;
using System.IO;

namespace IndigoMovieManager.Services
{
    public enum FolderCheckMode
    {
        Auto,
        Watch,
        Manual
    }

    internal static class FolderCheckService
    {
        public static string GetWatchSql(FolderCheckMode mode)
        {
            return mode switch
            {
                FolderCheckMode.Auto => "SELECT * FROM watch where auto = 1",
                FolderCheckMode.Watch => "SELECT * FROM watch where watch = 1",
                _ => "SELECT * FROM watch",
            };
        }

        public static List<(string Folder, bool Sub)> GetFoldersToCheck(DataTable watchData)
        {
            List<(string Folder, bool Sub)> folders = [];
            if (watchData == null)
            {
                return folders;
            }

            foreach (DataRow row in watchData.Rows)
            {
                string checkFolder = row["dir"].ToString();
                if (!Path.Exists(checkFolder))
                {
                    continue;
                }

                folders.Add((checkFolder, (long)row["sub"] == 1));
            }

            return folders;
        }

        public static bool IsFileRegistered(IEnumerable<MovieRecords> movieRecords, string fullPath)
        {
            return movieRecords.Any(x => string.Equals(x.Movie_Path, fullPath, StringComparison.Ordinal));
        }
    }
}
