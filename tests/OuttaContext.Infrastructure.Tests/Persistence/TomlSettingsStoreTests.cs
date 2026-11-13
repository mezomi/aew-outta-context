using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class TomlSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        OuttaSettings settings = Store().Load().ShouldSucceed();

        settings.ShouldBe(new OuttaSettings { ClipFolder = settings.ClipFolder });
        settings.MaxClipLength.ShouldHaveValue().Seconds.ShouldBe(45);
    }

    [Fact]
    public void Load_FileWrittenByThePythonVersion_IsRead()
    {
        // Arrange: exactly what the Python version's tomli_w wrote
        _temp.Write("settings.toml", """
            clip_folder = "C:\\Users\\mezom\\Videos\\Outta Context"
            episode_length = 600.0
            max_quality = 1080
            max_clip_length = 45.0
            default_storage = "drive"
            """);

        // Act
        OuttaSettings settings = Store().Load().ShouldSucceed();

        // Assert
        settings.ClipFolder.ShouldBe(@"C:\Users\mezom\Videos\Outta Context");
        settings.EpisodeLength.Seconds.ShouldBe(600);
        settings.MaxQuality.Height.ShouldBe(1080);
        settings.MaxClipLength.ShouldHaveValue().Seconds.ShouldBe(45);
        settings.DefaultStorage.ShouldHaveValue().Value.ShouldBe("drive");
    }

    [Fact]
    public void Change_Values_AreSavedAndReadBack()
    {
        Store().Change("episode-length", "12:30").ShouldSucceed();
        Store().Change("max-clip-length", "off").ShouldSucceed();
        Store().Change("cookies-from-browser", "firefox").ShouldSucceed();

        OuttaSettings settings = Store().Load().ShouldSucceed();

        settings.EpisodeLength.Seconds.ShouldBe(750);
        settings.MaxClipLength.HasNoValue.ShouldBeTrue();
        settings.CookiesFromBrowser.ShouldHaveValue().Value.ShouldBe("firefox");
    }

    [Fact]
    public void Change_BadValue_SavesNothing()
    {
        Store().Change("max-quality", "999").ShouldFailWith("settings.quality");

        File.Exists(_temp.File("settings.toml")).ShouldBeFalse();
    }

    [Fact]
    public void Load_BrokenFile_FailsWithWhereItIs()
    {
        _temp.Write("settings.toml", "clip_folder = [unclosed");

        Store().Load().ShouldFailWith("settings.file").Message.ShouldContain("not valid TOML");
    }

    [Theory]
    [InlineData("max_quality = 999", "max_quality")]
    [InlineData("episode_length = -5", "episode_length")]
    [InlineData("cookies_from_browser = \"lynx\"", "cookies_from_browser")]
    public void Load_ValueOutsideItsRules_FailsNamingTheKey(string line, string key)
    {
        _temp.Write("settings.toml", line);

        Store().Load().ShouldFailWith("settings.file").Message.ShouldContain(key);
    }

    private TomlSettingsStore Store() => new(new AppPaths(_temp.Path));
}
