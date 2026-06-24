using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// フォルダ走査用。DB の movie_path を一度だけ読み込み、O(1) で照合する。
    /// </summary>
    internal sealed class MoviePathRegistrationIndex
    {
        private readonly HashSet<string> _normalizedPaths;

        private MoviePathRegistrationIndex(HashSet<string> normalizedPaths)
        {
            _normalizedPaths = normalizedPaths;
        }

        public static MoviePathRegistrationIndex Load(string dbFullPath)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(dbFullPath) || !File.Exists(dbFullPath))
            {
                return new MoviePathRegistrationIndex(paths);
            }

            try
            {
                using SQLiteConnection connection = new($"Data Source={dbFullPath}");
                connection.Open();
                using SQLiteCommand cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT movie_path FROM movie";
                using SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0))
                    {
                        continue;
                    }

                    RegisterPath(paths, reader.GetString(0));
                }
            }
            catch
            {
                paths.Clear();
            }

            return new MoviePathRegistrationIndex(paths);
        }

        public bool IsRegistered(string fullPath)
        {
            string normalized = MediaPathNormalizer.Normalize(fullPath);
            return !string.IsNullOrEmpty(normalized) && _normalizedPaths.Contains(normalized);
        }

        public void Register(string fullPath) => RegisterPath(_normalizedPaths, fullPath);

        public static List<string> FindUnregisteredFiles(
            MoviePathRegistrationIndex index,
            string folder,
            bool recurseSubdirectories,
            string excludeExtSetting = null)
        {
            List<string> discovered = [];
            if (index == null || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return discovered;
            }

            foreach (FileInfo file in FolderCheckService.EnumerateMediaFiles(
                         new DirectoryInfo(folder),
                         recurseSubdirectories,
                         excludeExtSetting))
            {
                if (!index.IsRegistered(file.FullName))
                {
                    discovered.Add(file.FullName);
                }
            }

            return discovered;
        }

        private static void RegisterPath(HashSet<string> paths, string path)
        {
            string normalized = MediaPathNormalizer.Normalize(path);
            if (!string.IsNullOrEmpty(normalized))
            {
                paths.Add(normalized);
            }
        }
    }
}
