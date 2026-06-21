using System.Diagnostics;
using System.Windows;
using IndigoMovieManager.Data;

namespace IndigoMovieManager.Services
{
    internal sealed class ExternalPlayerLaunchRequest
    {
        public string PlayerProgram { get; init; }
        public string PlayerParam { get; init; }
        public string MoviePathQuoted { get; init; }
        public MovieRecords TargetRecord { get; init; }
        public int StartMsec { get; init; }
    }

    internal static class ExternalPlayerLauncher
    {
        public static ExternalPlayerLaunchRequest BuildRequest(
            string dbPlayerPrg,
            string dbPlayerParam,
            string defaultPlayerPath,
            string defaultPlayerParam,
            MovieRecords mv,
            string moviePathQuoted,
            int startMsec)
        {
            string playerPrg = string.IsNullOrEmpty(dbPlayerPrg) ? defaultPlayerPath : dbPlayerPrg;
            string playerParam = string.IsNullOrEmpty(dbPlayerParam) ? defaultPlayerParam : dbPlayerParam;

            if (!string.IsNullOrEmpty(playerParam))
            {
                playerParam = playerParam.Replace("<file>", $"{mv.Movie_Path}");
                playerParam = playerParam.Replace("<ms>", $"{startMsec}");
            }

            return new ExternalPlayerLaunchRequest
            {
                PlayerProgram = playerPrg,
                PlayerParam = playerParam,
                MoviePathQuoted = moviePathQuoted,
                TargetRecord = mv,
                StartMsec = startMsec,
            };
        }

        public static async Task LaunchAsync(ExternalPlayerLaunchRequest request, Window owner)
        {
            string arg = $"{request.MoviePathQuoted} {request.PlayerParam}";

            using Process ps1 = new();
            if (string.IsNullOrEmpty(request.PlayerProgram))
            {
                ps1.StartInfo.UseShellExecute = true;
                ps1.StartInfo.FileName = request.MoviePathQuoted;
            }
            else
            {
                ps1.StartInfo.Arguments = arg;
                ps1.StartInfo.FileName = request.PlayerProgram;
            }

            ps1.Start();

            string psName = ps1.ProcessName;
            foreach (Process p in Process.GetProcessesByName(psName))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    if (p.MainWindowTitle.Contains(request.TargetRecord.Movie_Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        p.Kill();
                        await p.WaitForExitAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        public static void ApplyPlaybackStats(MovieRecords mv, string dbFullPath)
        {
            mv.View_Count += 1;
            mv.Score += 1;
            DateTime now = DateTime.Now;
            DateTime result = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
            mv.Last_Date = result.ToString("yyyy-MM-dd HH:mm:ss");

            SQLite.UpdateMovieSingleColumn(dbFullPath, mv.Movie_Id, MovieColumn.Score, mv.Score);
            SQLite.UpdateMovieSingleColumn(dbFullPath, mv.Movie_Id, MovieColumn.View_Count, mv.View_Count);
            SQLite.UpdateMovieSingleColumn(dbFullPath, mv.Movie_Id, "last_date", result);
        }
    }
}
