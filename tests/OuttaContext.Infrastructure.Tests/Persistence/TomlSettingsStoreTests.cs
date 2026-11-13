using OuttaContext.Core.Errors;
using OuttaContext.Core.Settings;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class TomlSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        OuttaSettings settings = Store().Load();

        settings.ShouldBe(new OuttaSettings { ClipFolder = settings.ClipFolder });
        settings.MaxClipLength.ShouldBe(45);
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
        OuttaSettings settings = Store().Load();

        // Assert
        settings.ClipFolder.ShouldBe(@"C:\Users\mezom\Videos\Outta Context");
        (settings.EpisodeLength, settings.MaxQuality, settings.MaxClipLength, settings.DefaultStorage).ShouldBe((600, 1080, (double?)45, "drive"));
    }

    [Fact]
    public void Set_Value_IsSavedAndReadBack()
    {
        Store().Set("episode-length", "12:30");
        Store().Set("max-clip-length", "off");
        Store().Set("cookies-from-browser", "firefox");

        OuttaSettings settings = Store().Load();

        settings.EpisodeLength.ShouldBe(750);
        settings.MaxClipLength.ShouldBeNull();
        settings.CookiesFromBrowser.ShouldBe("firefox");
    }

    [Fact]
    public void Load_BrokenFile_ThrowsSettingsException()
    {
        _temp.Write("settings.toml", "clip_folder = [unclosed");

        Should.Throw<SettingsException>(() => Store().Load()).Message.ShouldContain("not valid TOML");
    }

    private TomlSettingsStore Store() => new(_temp.File("settings.toml"));
}
