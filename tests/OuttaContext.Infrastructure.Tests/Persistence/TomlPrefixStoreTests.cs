using OuttaContext.Core.Paths;
using OuttaContext.Core.Scraping;
using OuttaContext.Infrastructure.Persistence;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Persistence;

public sealed class TomlPrefixStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Add_Prefixes_AreSavedPerSite()
    {
        Store().Add("dailymotion", Prefix("https://geo.dailymotion.com/player/")).ShouldSucceed();
        Store().Add("youtube", Prefix("https://yt.example/")).ShouldSucceed();

        Store().ForSite("dailymotion").Select(p => p.Value).ShouldBe(["https://geo.dailymotion.com/player/"]);
        Store().All().Keys.ShouldBe(["dailymotion", "youtube"], ignoreOrder: true);
    }

    [Fact]
    public void Add_SamePrefixTwice_IsRefused()
    {
        Store().Add("dailymotion", Prefix("https://a/")).ShouldSucceed();

        Store().Add("dailymotion", Prefix("https://a/")).ShouldFailWith("prefix.exists");
    }

    [Fact]
    public void Remove_UnknownPrefix_FailsWithNotFound()
    {
        Store().Remove("youtube", Prefix("https://nope/")).ShouldFailWith("prefix.not_found");
    }

    [Fact]
    public void ForSite_FileWrittenByThePythonVersion_IsRead()
    {
        _temp.Write("prefixes.toml", "[prefixes]\ndailymotion = [\"https://players.example-cdn.com/\"]\n");

        Store().ForSite("dailymotion").Select(p => p.Value).ShouldBe(["https://players.example-cdn.com/"]);
    }

    private static LinkPrefix Prefix(string text) => LinkPrefix.Create(text).ShouldSucceed();

    private TomlPrefixStore Store() => new(new AppPaths(_temp.Path));
}
