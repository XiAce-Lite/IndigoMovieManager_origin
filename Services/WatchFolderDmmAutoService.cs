using System.Data;
using System.IO;
using static IndigoMovieManager.SQLite;

namespace IndigoMovieManager.Services
{
    internal static class WatchFolderDmmAutoService
    {
        public static void EnsureSchema(string dbFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath) || !File.Exists(dbFullPath))
            {
                return;
            }

            try
            {
                DataTable columns = GetData(dbFullPath, "PRAGMA table_info(watch)");
                bool hasDmmAuto = columns.AsEnumerable()
                    .Any(row => string.Equals(row["name"]?.ToString(), "dmm_auto", StringComparison.OrdinalIgnoreCase));
                if (hasDmmAuto)
                {
                    return;
                }

                using var connection = new System.Data.SQLite.SQLiteConnection($"Data Source={dbFullPath}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "ALTER TABLE watch ADD COLUMN dmm_auto integer not null default 0";
                cmd.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        public static bool IsEnabledForMediaPath(string dbFullPath, string mediaFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath) || string.IsNullOrWhiteSpace(mediaFullPath))
            {
                return false;
            }

            EnsureSchema(dbFullPath);

            try
            {
                DataTable watchRows = GetData(
                    dbFullPath,
                    "SELECT dir, sub FROM watch WHERE dmm_auto = 1");
                if (watchRows == null || watchRows.Rows.Count == 0)
                {
                    return false;
                }

                string normalizedFile = MediaPathNormalizer.Normalize(mediaFullPath);
                if (string.IsNullOrEmpty(normalizedFile))
                {
                    return false;
                }

                int bestLength = -1;
                bool matched = false;

                foreach (DataRow row in watchRows.Rows)
                {
                    string watchDir = row["dir"]?.ToString();
                    if (string.IsNullOrWhiteSpace(watchDir))
                    {
                        continue;
                    }

                    string normalizedDir = MediaPathNormalizer.Normalize(watchDir);
                    if (string.IsNullOrEmpty(normalizedDir))
                    {
                        continue;
                    }

                    bool includeSubfolders = Convert.ToInt64(row["sub"]) == 1;
                    if (!IsFileUnderWatchFolder(normalizedFile, normalizedDir, includeSubfolders))
                    {
                        continue;
                    }

                    if (normalizedDir.Length > bestLength)
                    {
                        bestLength = normalizedDir.Length;
                        matched = true;
                    }
                }

                return matched;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsFileUnderWatchFolder(
            string normalizedFilePath,
            string normalizedWatchDir,
            bool includeSubfolders)
        {
            if (string.IsNullOrWhiteSpace(normalizedFilePath) || string.IsNullOrWhiteSpace(normalizedWatchDir))
            {
                return false;
            }

            string dir = normalizedWatchDir.TrimEnd('\\', '/');
            string file = normalizedFilePath;

            if (!file.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (file.Length == dir.Length)
            {
                return false;
            }

            char separator = file[dir.Length];
            if (separator != '\\' && separator != '/')
            {
                return false;
            }

            if (includeSubfolders)
            {
                return true;
            }

            string relative = file[(dir.Length + 1)..];
            return !relative.Contains('\\') && !relative.Contains('/');
        }
    }
}
