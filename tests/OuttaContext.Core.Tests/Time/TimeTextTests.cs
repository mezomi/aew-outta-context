using OuttaContext.Core.Errors;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Tests.Time;

public sealed class TimeTextTests
{
    [Theory]
    [InlineData("90", 90)]
    [InlineData("0", 0)]
    [InlineData("1:30", 90)]
    [InlineData("01:05", 65)]
    [InlineData("1:02:10", 3730)]
    [InlineData("12.5", 12.5)]
    [InlineData("1:30.25", 90.25)]
    [InlineData(" 45 ", 45)]
    public void ParseSeconds_ValidTime_ReturnsSeconds(string text, double expected)
    {
        double seconds = TimeText.ParseSeconds(text);

        seconds.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1:")]
    [InlineData(":30")]
    [InlineData("1:60")]
    [InlineData("1:75:00")]
    [InlineData("1:2:3:4")]
    [InlineData("-5")]
    public void ParseSeconds_InvalidTime_ThrowsInvalidTimeRange(string text)
    {
        Should.Throw<InvalidTimeRangeException>(() => TimeText.ParseSeconds(text));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3730, "1:02:10")]
    [InlineData(-1, "0:00")]
    public void Format_Seconds_ReturnsClockText(double seconds, string expected)
    {
        TimeText.Format(seconds).ShouldBe(expected);
    }

    [Theory]
    [InlineData(20, "20s")]
    [InlineData(139, "2:19")]
    public void FormatLength_Seconds_UsesSecondsUnderAMinute(double seconds, string expected)
    {
        TimeText.FormatLength(seconds).ShouldBe(expected);
    }
}
