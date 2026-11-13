using OuttaContext.Core.Errors;
using OuttaContext.Core.Settings;

namespace OuttaContext.Core.Tests.Settings;

public sealed class SettingKeysTests
{
    private static readonly OuttaSettings Defaults = new();

    [Fact]
    public void Apply_EpisodeLength_ParsesClockText()
    {
        SettingKeys.Apply(Defaults, "episode-length", "12:30").EpisodeLength.ShouldBe(750);
    }

    [Theory]
    [InlineData("1:30", 90.0)]
    [InlineData("45", 45.0)]
    [InlineData("off", null)]
    [InlineData("0", null)]
    public void Apply_MaxClipLength_SetsCapOrTurnsItOff(string value, double? expected)
    {
        SettingKeys.Apply(Defaults, "max-clip-length", value).MaxClipLength.ShouldBe(expected);
    }

    [Fact]
    public void Apply_QualityWithP_IsAccepted()
    {
        SettingKeys.Apply(Defaults, "max-quality", "720p").MaxQuality.ShouldBe(720);
    }

    [Fact]
    public void Apply_UnderscoreKey_IsTheSameAsDash()
    {
        SettingKeys.Apply(Defaults, "default_storage", "drive").DefaultStorage.ShouldBe("drive");
    }

    [Theory]
    [InlineData("Firefox", "firefox")]
    [InlineData("chrome:Profile 1", "chrome:profile 1")]
    [InlineData("off", null)]
    public void Apply_CookiesFromBrowser_StoresLowerCaseBrowser(string value, string? expected)
    {
        SettingKeys.Apply(Defaults, "cookies-from-browser", value).CookiesFromBrowser.ShouldBe(expected);
    }

    [Theory]
    [InlineData("episode-length", "ten")]
    [InlineData("episode-length", "5")]
    [InlineData("max-quality", "999")]
    [InlineData("max-clip-length", "long")]
    [InlineData("cookies-from-browser", "netscape")]
    [InlineData("colour", "red")]
    public void Apply_BadValue_ThrowsSettingsExceptionWithHint(string key, string value)
    {
        var error = Should.Throw<SettingsException>(() => SettingKeys.Apply(Defaults, key, value));

        error.Hint.ShouldNotBeEmpty();
    }

    [Fact]
    public void Display_Defaults_AreHumanReadable()
    {
        IReadOnlyDictionary<string, string> shown = SettingKeys.Display(Defaults);

        shown["episode-length"].ShouldBe("10:00");
        shown["max-clip-length"].ShouldBe("45s");
        shown["max-quality"].ShouldBe("1080p");
        shown["default-storage"].ShouldBe("(none)");
    }
}
