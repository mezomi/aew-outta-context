using OuttaContext.Core.Time;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Time;

public sealed class VideoTimeTests
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
    public void Parse_ValidTime_ReturnsSeconds(string text, double expected)
    {
        VideoTime.Parse(text).ShouldSucceed().Seconds.ShouldBe(expected);
    }

    [Theory]
    [InlineData("", "time.invalid")]
    [InlineData("abc", "time.invalid")]
    [InlineData("1:", "time.invalid")]
    [InlineData(":30", "time.invalid")]
    [InlineData("1:2:3:4", "time.invalid")]
    [InlineData("-5", "time.invalid")]
    [InlineData("1:60", "time.seconds_over_59")]
    [InlineData("1:75:00", "time.minutes_over_59")]
    public void Parse_InvalidTime_FailsWithReason(string text, string code)
    {
        VideoTime.Parse(text).ShouldFailWith(code).Hint.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FromSeconds_NegativeOrNotANumber_Fails(double seconds)
    {
        VideoTime.FromSeconds(seconds).ShouldFailWith("time.negative");
    }

    [Fact]
    public void FromTrusted_Negative_ThrowsBecauseItIsABug()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => VideoTime.FromTrusted(-1));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3730, "1:02:10")]
    public void ToClock_Seconds_ReturnsClockText(double seconds, string expected)
    {
        VideoTime.FromTrusted(seconds).ToClock().ShouldBe(expected);
    }

    [Theory]
    [InlineData(20, "20s")]
    [InlineData(139, "2:19")]
    public void ToShortLength_Seconds_UsesSecondsUnderAMinute(double seconds, string expected)
    {
        VideoTime.FromTrusted(seconds).ToShortLength().ShouldBe(expected);
    }

    [Fact]
    public void MinusOrZero_LargerOther_IsZeroNotNegative()
    {
        VideoTime.FromTrusted(5).MinusOrZero(VideoTime.FromTrusted(8)).ShouldBe(VideoTime.Zero);
    }

    [Fact]
    public void Equality_SameSeconds_AreEqualValues()
    {
        VideoTime.Parse("1:30").ShouldSucceed().ShouldBe(VideoTime.FromTrusted(90));
    }

    [Fact]
    public void CompareTo_Times_OrderBySeconds()
    {
        VideoTime.FromTrusted(10).CompareTo(VideoTime.FromTrusted(20)).ShouldBeLessThan(0);
    }
}
