using OuttaContext.Infrastructure.FFmpeg;

namespace OuttaContext.Infrastructure.Tests.FFmpeg;

public sealed class FfmpegProgressTests
{
    [Theory]
    [InlineData("out_time_us=1500000", 1.5)]
    [InlineData("out_time_ms=2000000", 2.0)]
    [InlineData("out_time_us=N/A", null)]
    [InlineData("frame=10", null)]
    public void ParseOutTime_ProgressLine_ReturnsSeconds(string line, double? expected)
    {
        FfmpegProgress.ParseOutTime(line).ShouldBe(expected);
    }

    [Theory]
    [InlineData("30000/1001", 29.97)]
    [InlineData("25", 25.0)]
    [InlineData("0/0", null)]
    [InlineData("1/0", null)]
    [InlineData(null, null)]
    public void ParseRate_FfprobeRate_ReturnsFramesPerSecond(string? rate, double? expected)
    {
        double? fps = FfmpegProgress.ParseRate(rate);

        if (expected is null)
        {
            fps.ShouldBeNull();
        }
        else
        {
            fps!.Value.ShouldBe(expected.Value, 0.01);
        }
    }
}
