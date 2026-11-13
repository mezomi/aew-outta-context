using OuttaContext.Core.Settings;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Settings;

public sealed class SettingKeysTests
{
    private static readonly OuttaSettings Defaults = new();

    [Fact]
    public void Apply_EpisodeLength_ParsesClockText()
    {
        SettingKeys.Apply(Defaults, "episode-length", "12:30").ShouldSucceed().EpisodeLength.Seconds.ShouldBe(750);
    }

    [Theory]
    [InlineData("1:30", 90.0)]
    [InlineData("45", 45.0)]
    [InlineData("off", null)]
    [InlineData("0", null)]
    public void Apply_MaxClipLength_SetsCapOrTurnsItOff(string value, double? expected)
    {
        OuttaSettings updated = SettingKeys.Apply(Defaults, "max-clip-length", value).ShouldSucceed();

        updated.MaxClipLength.Map(cap => (double?)cap.Seconds).GetValueOrDefault().ShouldBe(expected);
    }

    [Fact]
    public void Apply_QualityWithP_IsAccepted()
    {
        SettingKeys.Apply(Defaults, "max-quality", "720p").ShouldSucceed().MaxQuality.Height.ShouldBe(720);
    }

    [Fact]
    public void Apply_UnderscoreKey_IsTheSameAsDash()
    {
        SettingKeys.Apply(Defaults, "default_storage", "drive").ShouldSucceed().DefaultStorage.ShouldHaveValue().Value.ShouldBe("drive");
    }

    [Theory]
    [InlineData("Firefox", "firefox")]
    [InlineData("chrome:Profile 1", "chrome:profile 1")]
    [InlineData("off", null)]
    public void Apply_CookiesFromBrowser_StoresLowerCaseBrowser(string value, string? expected)
    {
        OuttaSettings updated = SettingKeys.Apply(Defaults, "cookies-from-browser", value).ShouldSucceed();

        updated.CookiesFromBrowser.Map(browser => browser.Value).GetValueOrDefault().ShouldBe(expected);
    }

    [Theory]
    [InlineData("episode-length", "ten", "settings.not_a_length")]
    [InlineData("episode-length", "5", "settings.episode_too_short")]
    [InlineData("max-quality", "999", "settings.quality")]
    [InlineData("max-clip-length", "long", "settings.not_a_length")]
    [InlineData("cookies-from-browser", "netscape", "settings.browser")]
    [InlineData("default-storage", "my drive!", "storage.invalid_name")]
    [InlineData("destination", "", "settings.folder_empty")]
    [InlineData("colour", "red", "settings.unknown_key")]
    public void Apply_BadValue_FailsWithHint(string key, string value, string code)
    {
        SettingKeys.Apply(Defaults, key, value).ShouldFailWith(code).Hint.ShouldNotBeEmpty();
    }

    [Fact]
    public void Display_Defaults_AreHumanReadable()
    {
        IReadOnlyDictionary<string, string> shown = SettingKeys.Display(Defaults);

        shown["episode-length"].ShouldBe("10:00");
        shown["max-clip-length"].ShouldBe("45s");
        shown["max-quality"].ShouldBe("1080p");
        shown["default-storage"].ShouldBe("(none)");
        shown["cookies-from-browser"].ShouldBe("off");
    }
}
