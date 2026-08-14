using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public sealed class FfmpegPathResolverTests
{
    [Fact]
    public void TryResolve_finds_ffmpeg_on_path()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "imm-ffmpeg-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string fakeFfmpeg = Path.Combine(tempDir, "ffmpeg.exe");
        File.WriteAllText(fakeFfmpeg, string.Empty);

        string previousPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        string previousImmPath = Environment.GetEnvironmentVariable("IMM_FFMPEG_EXE_PATH");
        string previousSettingsPath = Properties.Settings.Default.FfmpegExePath;

        try
        {
            Environment.SetEnvironmentVariable("IMM_FFMPEG_EXE_PATH", null);
            Properties.Settings.Default.FfmpegExePath = "";
            Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + previousPath);

            Assert.True(FfmpegPathResolver.TryResolve(out string resolved));
            Assert.Equal(Path.GetFullPath(fakeFfmpeg), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("IMM_FFMPEG_EXE_PATH", previousImmPath);
            Properties.Settings.Default.FfmpegExePath = previousSettingsPath;

            if (File.Exists(fakeFfmpeg))
            {
                File.Delete(fakeFfmpeg);
            }

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir);
            }
        }
    }

    [Fact]
    public void TryResolveFfprobe_finds_ffprobe_on_path_when_not_sibling_of_ffmpeg()
    {
        string ffmpegDir = Path.Combine(Path.GetTempPath(), "imm-ffmpeg-only-" + Guid.NewGuid().ToString("N"));
        string ffprobeDir = Path.Combine(Path.GetTempPath(), "imm-ffprobe-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ffmpegDir);
        Directory.CreateDirectory(ffprobeDir);

        string fakeFfmpeg = Path.Combine(ffmpegDir, "ffmpeg.exe");
        string fakeFfprobe = Path.Combine(ffprobeDir, "ffprobe.exe");
        File.WriteAllText(fakeFfmpeg, string.Empty);
        File.WriteAllText(fakeFfprobe, string.Empty);

        string previousPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        string previousImmPath = Environment.GetEnvironmentVariable("IMM_FFMPEG_EXE_PATH");
        string previousSettingsPath = Properties.Settings.Default.FfmpegExePath;

        try
        {
            Environment.SetEnvironmentVariable("IMM_FFMPEG_EXE_PATH", ffmpegDir);
            Properties.Settings.Default.FfmpegExePath = "";
            Environment.SetEnvironmentVariable("PATH", ffprobeDir + Path.PathSeparator + previousPath);

            Assert.True(FfmpegPathResolver.TryResolveFfprobe(out string resolved));
            Assert.Equal(Path.GetFullPath(fakeFfprobe), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            Environment.SetEnvironmentVariable("IMM_FFMPEG_EXE_PATH", previousImmPath);
            Properties.Settings.Default.FfmpegExePath = previousSettingsPath;

            if (File.Exists(fakeFfmpeg))
            {
                File.Delete(fakeFfmpeg);
            }

            if (File.Exists(fakeFfprobe))
            {
                File.Delete(fakeFfprobe);
            }

            if (Directory.Exists(ffmpegDir))
            {
                Directory.Delete(ffmpegDir);
            }

            if (Directory.Exists(ffprobeDir))
            {
                Directory.Delete(ffprobeDir);
            }
        }
    }
}
