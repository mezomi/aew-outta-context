using OuttaContext.Core.Errors;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class TomlPrefixStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Add_Prefixes_AreSavedPerSite()
    {
        Store().Add("dailymotion", "https://geo.dailymotion.com/player/");
        Store().Add("youtube", "https://yt.example/");

        Store().ForSite("dailymotion").ShouldBe(["https://geo.dailymotion.com/player/"]);
        Store().All().Keys.ShouldBe(["dailymotion", "youtube"], ignoreOrder: true);
    }

    [Fact]
    public void Add_SamePrefixTwice_IsRefused()
    {
        Store().Add("dailymotion", "https://a/");

        Should.Throw<AlreadyExistsException>(() => Store().Add("dailymotion", "https://a/"));
    }

    [Fact]
    public void Add_NotALink_IsRefusedWithAnExample()
    {
        Should.Throw<SettingsException>(() => Store().Add("dailymotion", "geo.dailymotion.com")).Hint.ShouldContain("https://");
    }

    [Fact]
    public void Remove_UnknownPrefix_ThrowsNotFound()
    {
        Should.Throw<NotFoundException>(() => Store().Remove("youtube", "https://nope/"));
    }

    [Fact]
    public void ForSite_FileWrittenByThePythonVersion_IsRead()
    {
        _temp.Write("prefixes.toml", "[prefixes]\ndailymotion = [\"https://players.example-cdn.com/\"]\n");

        Store().ForSite("dailymotion").ShouldBe(["https://players.example-cdn.com/"]);
    }

    private TomlPrefixStore Store() => new(_temp.File("prefixes.toml"));
}
