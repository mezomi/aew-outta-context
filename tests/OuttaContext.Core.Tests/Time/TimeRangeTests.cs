using OuttaContext.Core.Errors;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Tests.Time;

public sealed class TimeRangeTests
{
    [Theory]
    [InlineData("90-112", 90, 112)]
    [InlineData("1:30-1:52", 90, 112)]
    [InlineData("1:02:10-1:02:30", 3730, 3750)]
    [InlineData("0:45 - 1:05", 45, 65)]
    public void Parse_ValidRange_ReturnsStartAndEnd(string text, double start, double end)
    {
        TimeRange range = TimeRange.Parse(text);

        range.ShouldBe(new TimeRange(start, end));
        range.Length.ShouldBe(end - start);
    }

    [Theory]
    [InlineData("1:30")]
    [InlineData("1:52-1:30")]
    [InlineData("10-10")]
    [InlineData("1-2-3")]
    [InlineData("a-b")]
    public void Parse_InvalidRange_ThrowsWithHint(string text)
    {
        var error = Should.Throw<InvalidTimeRangeException>(() => TimeRange.Parse(text));

        error.Hint.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_EndBeforeStart_SaysEndMustBeAfterStart()
    {
        Should.Throw<InvalidTimeRangeException>(() => TimeRange.Parse("1:52-1:30")).Message.ShouldContain("must be after");
    }

    [Theory]
    [InlineData("0:10-0:20", 30.0)]
    [InlineData("0:10-0:30", 30.0)]
    [InlineData("0:10-0:20", null)]
    public void EnsureWithin_RangeInsideVideo_DoesNotThrow(string text, double? duration)
    {
        Should.NotThrow(() => TimeRange.Parse(text).EnsureWithin(duration));
    }

    [Theory]
    [InlineData("0:10-0:40", "End 0:40")]
    [InlineData("0:35-0:40", "Start 0:35")]
    public void EnsureWithin_RangePastTheEnd_ThrowsRangeOutsideVideo(string text, string expected)
    {
        Should.Throw<RangeOutsideVideoException>(() => TimeRange.Parse(text).EnsureWithin(30)).Message.ShouldContain(expected);
    }

    [Fact]
    public void ToString_Range_UsesClockText()
    {
        TimeRange.Parse("45-65").ToString().ShouldBe("0:45-1:05");
    }
}
