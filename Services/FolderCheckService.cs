using System.Data;
using System.Diagnostics;
using System.IO;

namespace IndigoMovieManager.Services
{
    public enum FolderCheckMode
    {
        Auto,
        Watch,
        Manual
    }

    internal sealed class FolderCheckScanResult
    {
        public bool RegisteredAny { get; init; }
        public bool FoundUnregistered { get; init; }
        public List<QueueObj> AddedThumbnailWork { get; init; } = [];
    }

    internal sealed class FolderCheckScanCallbacks
    {
        public Func<bool> IsStillActive { get; init; }
        public Func<string, bool> TryEnterRegistrationGate { get; init; }
        public Action<string> ExitRegistrationGate { get; init; }
        public Action<long> OnMovieRegistered { get; init; }
        public Func<int, string, Task> ReportProgressAsync { get; init; }
    }

    internal static class FolderCheckService
    {
        public const int IoRetryDelayMs = 1000;
        public const int FolderCompletedDelayMs = 100;

        public static string FormatScanningMessage(string folder) =>
            $"{folder} 監視実施中…";

        public static string FormatHasUpdatesMessage(string folder) =>
            $"{folder} に更新あり。";

        public static string FormatCompletedMessage(string folder) =>
            $"{folder} 監視完了";

        public static bool ShouldApplyResults(bool registeredAny, bool foundUnregistered) =>
            registeredAny || foundUnregistered;

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

        public static IEnumerable<FileInfo> EnumerateMediaFiles(
            DirectoryInfo directory,
            bool recurseSubdirectories,
            string excludeExtSetting = null)
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
                if (!MediaExtensionSettings.ShouldScanFile(file.FullName, checkExt, excludeExtSetting))
                {
                    continue;
                }

                yield return file;
            }
        }

        /// <summary>
        /// 監視フォルダを走査し、未登録ファイルを登録する。進捗・世代判定は callbacks 経由。
        /// </summary>
        public static async Task<FolderCheckScanResult> ScanAndRegisterAsync(
            string dbFullPath,
            IReadOnlyList<(string Folder, bool Sub)> folders,
            string excludeExt,
            MoviePathRegistrationIndex pathIndex,
            FolderCheckScanCallbacks callbacks)
        {
            callbacks ??= new FolderCheckScanCallbacks();
            Func<bool> isStillActive = callbacks.IsStillActive ?? (() => true);
            Func<string, bool> tryEnter = callbacks.TryEnterRegistrationGate ?? (_ => true);
            Action<string> exitGate = callbacks.ExitRegistrationGate ?? (_ => { });
            Action<long> onRegistered = callbacks.OnMovieRegistered ?? (_ => { });
            Func<int, string, Task> report =
                callbacks.ReportProgressAsync ?? ((_, _) => Task.CompletedTask);

            bool registeredAny = false;
            bool foundUnregistered = false;
            List<QueueObj> addFiles = [];

            pathIndex ??= MoviePathRegistrationIndex.Load(dbFullPath);
            folders ??= [];

            for (int folderIndex = 0; folderIndex < folders.Count; folderIndex++)
            {
                if (!isStillActive())
                {
                    return BuildResult(registeredAny, foundUnregistered, addFiles);
                }

                (string checkFolder, bool sub) = folders[folderIndex];
                await report(folderIndex, FormatScanningMessage(checkFolder)).ConfigureAwait(false);

                List<string> unregisteredFiles;
                try
                {
                    unregisteredFiles = await Task.Run(() =>
                            MoviePathRegistrationIndex.FindUnregisteredFiles(
                                pathIndex,
                                checkFolder,
                                sub,
                                excludeExt))
                        .ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (e is IOException)
                    {
                        await Task.Delay(IoRetryDelayMs).ConfigureAwait(false);
                    }

                    unregisteredFiles = [];
                }

                if (!isStillActive())
                {
                    return BuildResult(registeredAny, foundUnregistered, addFiles);
                }

                if (unregisteredFiles.Count > 0)
                {
                    foundUnregistered = true;
                    await report(folderIndex, FormatHasUpdatesMessage(checkFolder)).ConfigureAwait(false);
                }

                foreach (string fileFullPath in unregisteredFiles)
                {
                    if (!isStillActive())
                    {
                        return BuildResult(registeredAny, foundUnregistered, addFiles);
                    }

                    try
                    {
                        string normalizedPath = MediaPathNormalizer.Normalize(fileFullPath);
                        if (string.IsNullOrWhiteSpace(normalizedPath) || !tryEnter(normalizedPath))
                        {
                            continue;
                        }

                        try
                        {
                            MovieInfo mvi = await MovieRegistrationHelper
                                .TryRegisterDiscoveredFileAsync(dbFullPath, fileFullPath)
                                .ConfigureAwait(false);
                            if (mvi == null)
                            {
                                continue;
                            }

                            pathIndex.Register(mvi.MoviePath);
                            registeredAny = true;
                            onRegistered(mvi.MovieId);
                            addFiles.Add(new QueueObj
                            {
                                MovieId = mvi.MovieId,
                                MovieFullPath = mvi.MoviePath,
                                DbFullPath = dbFullPath,
                            });
                        }
                        finally
                        {
                            exitGate(normalizedPath);
                        }
                    }
                    catch (Exception)
                    {
#if DEBUG
                        Debug.WriteLine(
                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [folder-check] skip {fileFullPath}");
#endif
                    }
                }

                if (!isStillActive())
                {
                    return BuildResult(registeredAny, foundUnregistered, addFiles);
                }

                await report(folderIndex + 1, FormatCompletedMessage(checkFolder)).ConfigureAwait(false);
                await Task.Delay(FolderCompletedDelayMs).ConfigureAwait(false);
            }

            return BuildResult(registeredAny, foundUnregistered, addFiles);
        }

        private static FolderCheckScanResult BuildResult(
            bool registeredAny,
            bool foundUnregistered,
            List<QueueObj> addFiles)
        {
            return new FolderCheckScanResult
            {
                RegisteredAny = registeredAny,
                FoundUnregistered = foundUnregistered,
                AddedThumbnailWork = addFiles,
            };
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
