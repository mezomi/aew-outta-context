using OuttaContext.Core.Settings;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Settings;

public sealed class VideoQualityTests
{
    [Theory]
    [InlineData("1080", 1080)]
    [InlineData("720p", 720)]
    [InlineData(" 2160P ", 2160)]
    public void Parse_SupportedHeight_ReturnsIt(string text, int height)
    {
        VideoQuality.Parse(text).ShouldSucceed().Height.ShouldBe(height);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("hd")]
    [InlineData("-720")]
    public void Parse_UnsupportedValue_FailsListingTheChoices(string text)
    {
        VideoQuality.Parse(text).ShouldFailWith("settings.quality").Hint.ShouldContain("1080");
    }

    [Fact]
    public void ToString_Quality_AddsP()
    {
        VideoQuality.Default.ToString().ShouldBe("1080p");
    }
}
