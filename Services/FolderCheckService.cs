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

            string normalized = MediaPathNormalizer.Normalize(fullPath);

            return movieRecords.Any(x =>

                string.Equals(

                    MediaPathNormalizer.Normalize(x.Movie_Path),

                    normalized,

                    StringComparison.OrdinalIgnoreCase));

        }



        /// <summary>

        /// 走査・監視での新規登録要否は DB のみを正とする（UI 上の MovieRecs は遅延や削除直後で古いことがある）。

        /// </summary>

        public static bool ShouldRegisterDiscoveredFile(string dbFullPath, string fullPath)

        {

            return !IsFileRegisteredInDb(dbFullPath, fullPath);

        }



        public static bool IsFileRegisteredInDb(string dbFullPath, string fullPath)

        {

            if (string.IsNullOrWhiteSpace(dbFullPath) || string.IsNullOrWhiteSpace(fullPath))

            {

                return false;

            }



            string normalizedPath = MediaPathNormalizer.Normalize(fullPath);

            if (string.IsNullOrEmpty(normalizedPath))

            {

                return false;

            }



            try

            {

                using System.Data.SQLite.SQLiteConnection connection = new($"Data Source={dbFullPath}");

                connection.Open();

                using System.Data.SQLite.SQLiteCommand cmd = connection.CreateCommand();

                cmd.CommandText =

                    "SELECT movie_path FROM movie " +

                    "WHERE lower(movie_path) = lower(@path) " +

                    "   OR lower(movie_path) = lower(@normalized)";

                cmd.Parameters.AddWithValue("@path", fullPath);

                cmd.Parameters.AddWithValue("@normalized", normalizedPath);

                using System.Data.SQLite.SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read())

                {

                    string storedPath = reader.GetString(0);

                    if (PathsEquivalent(storedPath, fullPath) || PathsEquivalent(storedPath, normalizedPath))

                    {

                        return true;

                    }

                }



                return false;

            }

            catch

            {

                return false;

            }

        }



        public static IEnumerable<FileInfo> EnumerateMediaFiles(DirectoryInfo directory, bool recurseSubdirectories)

        {

            if (directory == null)

            {

                yield break;

            }



            EnumerationOptions options = new()

            {

                RecurseSubdirectories = recurseSubdirectories,

                IgnoreInaccessible = true,

                AttributesToSkip = FileAttributes.System,

            };



            IEnumerable<FileInfo> files;

            try

            {

                files = directory.EnumerateFiles("*", options);

            }

            catch

            {

                yield break;

            }



            string checkExt = Properties.Settings.Default.CheckExt;

            foreach (FileInfo file in files)

            {

                if (!MediaExtensionSettings.MatchesExtension(file.FullName, checkExt))

                {

                    continue;

                }



                yield return file;

            }

        }



        private static bool PathsEquivalent(string left, string right)

        {

            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))

            {

                return false;

            }



            return string.Equals(

                MediaPathNormalizer.Normalize(left),

                MediaPathNormalizer.Normalize(right),

                StringComparison.OrdinalIgnoreCase);

        }

    }

}


