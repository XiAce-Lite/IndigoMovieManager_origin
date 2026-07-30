using IndigoMovieManager.Services;
using Xunit;

namespace IndigoMovieManager.Tests;

public class AppFileLoggerTests
{
    [Fact]
    public void Initialize_and_log_methods_write_log_file()
    {
        string logDir = Path.Combine(Path.GetTempPath(), $"imm-log-{Guid.NewGuid():N}");

        try
        {
            AppFileLogger.LogDirectoryOverride = logDir;
            AppFileLogger.ResetForTests();

            AppFileLogger.LogDirectoryOverride = logDir;
            AppFileLogger.Initialize();
            AppFileLogger.LogInfo("test", "hello");
            AppFileLogger.LogError(new InvalidOperationException("boom"), "test");

            string path = AppFileLogger.CurrentLogFilePath;
            Assert.True(File.Exists(path));

            string contents = File.ReadAllText(path);
            Assert.Contains("log initialized", contents);
            Assert.Contains("INFO", contents);
            Assert.Contains("hello", contents);
            Assert.Contains("ERROR", contents);
            Assert.Contains("boom", contents);
        }
        finally
        {
            AppFileLogger.ResetForTests();
            if (Directory.Exists(logDir))
            {
                Directory.Delete(logDir, recursive: true);
            }
        }
    }
}
