using System.Diagnostics;
using System.IO;
using static IndigoMovieManager.SQLite;

namespace IndigoMovieManager.Services
{
    internal static class MovieRegistrationHelper
    {
        public static bool TryCreateMovieInfo(string fileFullPath, out MovieInfo movieInfo, bool noHash = false)
        {
            movieInfo = null;
            if (string.IsNullOrWhiteSpace(fileFullPath) || !File.Exists(fileFullPath))
            {
                return false;
            }

            try
            {
                movieInfo = new MovieInfo(fileFullPath, noHash);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] MovieInfo failed: {fileFullPath} : {ex.Message}");
                return false;
            }
        }

        public static async Task<MovieInfo> TryRegisterDiscoveredFileAsync(string dbFullPath, string fileFullPath)
        {
            if (string.IsNullOrWhiteSpace(dbFullPath)
                || !TryCreateMovieInfo(fileFullPath, out MovieInfo movieInfo))
            {
                return null;
            }

            try
            {
                await InsertMovieTable(dbFullPath, movieInfo).ConfigureAwait(false);
                return movieInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} : [register] InsertMovieTable failed: {fileFullPath} : {ex.Message}");
                return null;
            }
        }
    }
}
