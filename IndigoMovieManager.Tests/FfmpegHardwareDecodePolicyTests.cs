using IndigoMovieManager.Thumbnail;
using Xunit;

namespace IndigoMovieManager.Tests;

public class FfmpegHardwareDecodePolicyTests
{
    [Fact]
    public void ParseHwaccelsOutput_skips_header_and_blank_lines()
    {
        const string output = """
            Hardware acceleration methods:
            cuda
            dxva2
            qsv

            d3d11va
            """;

        IReadOnlyList<string> actual = FfmpegHardwareDecodePolicy.ParseHwaccelsOutput(output);

        Assert.Equal(["cuda", "dxva2", "qsv", "d3d11va"], actual);
    }

    [Theory]
    [InlineData("Cuda", "cuda")]
    [InlineData("Qsv", "qsv")]
    [InlineData("D3d11va", "d3d11va")]
    [InlineData("Dxva2", "dxva2")]
    [InlineData("Off", "")]
    [InlineData("Auto", "")]
    public void GetHwaccelName_maps_mode_to_ffmpeg_name(string modeName, string expected)
    {
        Enum.TryParse(modeName, out FfmpegHardwareDecodeMode mode);
        Assert.Equal(expected, FfmpegHardwareDecodePolicy.GetHwaccelName(mode));
    }

    [Fact]
    public void GetConfiguredMode_defaults_to_off()
    {
        Assert.Equal(FfmpegHardwareDecodeMode.Off, FfmpegHardwareDecodePolicy.GetConfiguredMode());
    }

    [Fact]
    public void GetModesToAttempt_returns_empty_when_off()
    {
        IReadOnlyList<FfmpegHardwareDecodeMode> modes =
            FfmpegHardwareDecodePolicy.GetModesToAttempt(@"C:\ffmpeg\ffmpeg.exe");

        Assert.Empty(modes);
    }
}
