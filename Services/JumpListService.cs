using System.IO;
using System.Windows;
using System.Windows.Shell;

namespace IndigoMovieManager.Services
{
    /// <summary>
    /// タスクバー ジャンプリストをアプリの Recent と同期する（JumpTask・関連付け不要）。
    /// </summary>
    internal static class JumpListService
    {
        private const string RecentCategory = "最近使ったファイル";

        public static void SyncRecentFiles(IEnumerable<string> recentFilesNewestFirst)
        {
            try
            {
                Application app = Application.Current;
                if (app == null)
                {
                    return;
                }

                string appPath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
                {
                    return;
                }

                string workingDirectory = Path.GetDirectoryName(appPath) ?? "";
                var jumpList = new JumpList
                {
                    ShowRecentCategory = false,
                    ShowFrequentCategory = false
                };

                if (recentFilesNewestFirst != null)
                {
                    foreach (string path in recentFilesNewestFirst)
                    {
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        {
                            continue;
                        }

                        jumpList.JumpItems.Add(new JumpTask
                        {
                            Title = Path.GetFileName(path),
                            Description = path,
                            ApplicationPath = appPath,
                            Arguments = QuoteArgument(path),
                            WorkingDirectory = workingDirectory,
                            CustomCategory = RecentCategory
                        });
                    }
                }

                JumpList.SetJumpList(app, jumpList);
            }
            catch
            {
                // ジャンプリスト失敗で本体操作を止めない。
            }
        }

        internal static string QuoteArgument(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "\"\"";
            }

            if (path.Contains('"'))
            {
                path = path.Replace("\"", "\\\"");
            }

            return $"\"{path}\"";
        }
    }
}
