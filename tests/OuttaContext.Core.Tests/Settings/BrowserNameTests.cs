using OuttaContext.Core.Settings;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Settings;

public sealed class BrowserNameTests
{
    [Theory]
    [InlineData("firefox", "firefox")]
    [InlineData("Edge", "edge")]
    [InlineData("chrome:Profile 1", "chrome:profile 1")]
    public void Parse_SupportedBrowser_IsLowerCased(string text, string expected)
    {
        BrowserName.Parse(text).ShouldSucceed().Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("lynx")]
    [InlineData("")]
    public void Parse_UnknownBrowser_Fails(string text)
    {
        BrowserName.Parse(text).ShouldFailWith("settings.browser");
    }
}
