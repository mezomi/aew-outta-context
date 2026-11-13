using OuttaContext.Core.Time;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Core.Tests.Time;

public sealed class ClipTimeSpanTests
{
    [Theory]
    [InlineData("90-112", 90, 112)]
    [InlineData("1:30-1:52", 90, 112)]
    [InlineData("1:02:10-1:02:30", 3730, 3750)]
    [InlineData("0:45 - 1:05", 45, 65)]
    public void Parse_ValidRange_ReturnsStartEndAndLength(string text, double start, double end)
    {
        ClipTimeSpan span = ClipTimeSpan.Parse(text).ShouldSucceed();

        span.Start.Seconds.ShouldBe(start);
        span.End.Seconds.ShouldBe(end);
        span.Length.Seconds.ShouldBe(end - start);
    }

    [Theory]
    [InlineData("1:30", "range.invalid")]
    [InlineData("1-2-3", "range.invalid")]
    [InlineData("a-b", "time.invalid")]
    [InlineData("1:52-1:30", "range.end_before_start")]
    [InlineData("10-10", "range.end_before_start")]
    public void Parse_InvalidRange_FailsWithHint(string text, string code)
    {
        ClipTimeSpan.Parse(text).ShouldFailWith(code).Hint.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_EndBeforeStart_SaysEndMustBeAfterStart()
    {
        ClipTimeSpan.Parse("1:52-1:30").ShouldFailWith("range.end_before_start").Message.ShouldContain("must be after");
    }

    [Theory]
    [InlineData("0:10-0:20")]
    [InlineData("0:10-0:30")]
    [InlineData("0:10-0:30.4")]
    public void FitsIn_SpanInsideVideo_Succeeds(string text)
    {
        Values.Span(text).FitsIn(Values.Time(30)).ShouldSucceed();
    }

    [Fact]
    public void FitsIn_UnknownLength_Succeeds()
    {
        Values.Span("5:00-5:10").FitsIn(Maybe<VideoTime>.None).ShouldSucceed();
    }

    [Theory]
    [InlineData("0:10-0:40", "End 0:40")]
    [InlineData("0:35-0:40", "Start 0:35")]
    public void FitsIn_SpanPastTheEnd_FailsWithRangeOutsideVideo(string text, string expected)
    {
        Values.Span(text).FitsIn(Values.Time(30)).ShouldFailWith("range.outside_video").Message.ShouldContain(expected);
    }

    [Fact]
    public void ToString_Span_UsesClockText()
    {
        Values.Span("45-65").ToString().ShouldBe("0:45-1:05");
    }

    [Fact]
    public void Equality_SameStartAndEnd_AreEqualValues()
    {
        Values.Span("90-112").ShouldBe(Values.Span("1:30-1:52"));
    }
}
