using System.IO;
using System.Text;

namespace IndigoMovieManager.Services
{
    internal static class AppFileLogger
    {
        private static readonly object Sync = new();
        private static string _logDirectory;

        internal static string LogDirectoryOverride { get; set; }

        internal static string CurrentLogFilePath
        {
            get
            {
                lock (Sync)
                {
                    return ResolveLogFilePath();
                }
            }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                EnsureLogDirectoryExists();
                WriteLineCore("INFO", "startup", "log initialized");
            }
        }

        public static void LogInfo(string source, string message) =>
            Log("INFO", source, message);

        public static void LogError(string source, string message) =>
            Log("ERROR", source, message);

        public static void LogError(Exception exception, string source, string message = null)
        {
            if (exception == null)
            {
                Log("ERROR", source, message);
                return;
            }

            string fullMessage = string.IsNullOrWhiteSpace(message)
                ? exception.ToString()
                : $"{message}{Environment.NewLine}{exception}";
            Log("ERROR", source, fullMessage);
        }

        internal static void ResetForTests()
        {
            lock (Sync)
            {
                _logDirectory = null;
                LogDirectoryOverride = null;
            }
        }

        private static void Log(string level, string source, string message)
        {
            lock (Sync)
            {
                EnsureLogDirectoryExists();
                WriteLineCore(level, source, message);
            }
        }

        private static void WriteLineCore(string level, string source, string message)
        {
            try
            {
                string line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{level}\t{Normalize(source)}\t{Normalize(message)}{Environment.NewLine}";
                File.AppendAllText(ResolveLogFilePath(), line, new UTF8Encoding(false));
            }
            catch
            {
                // ログ書き込み失敗で本体挙動は変えない。
            }
        }

        private static void EnsureLogDirectoryExists()
        {
            if (!string.IsNullOrEmpty(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
                return;
            }

            string root = LogDirectoryOverride;
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IndigoMovieManager",
                    "logs");
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Path.GetTempPath(), "IndigoMovieManager", "logs");
            }

            Directory.CreateDirectory(root);
            _logDirectory = root;
        }

        private static string ResolveLogFilePath() =>
            Path.Combine(_logDirectory ?? "", $"app-{DateTime.Today:yyyyMMdd}.log");

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? "-"
                : value.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
