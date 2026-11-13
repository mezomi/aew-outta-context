using OuttaContext.Core.Errors;
using OuttaContext.Core.Scraping;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Scraping;

public sealed class ScrapeHandlerTests
{
    private readonly FakePageFetcher _fetcher = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly RecordingReporter _reporter = new();
    private readonly InMemoryPrefixStore _prefixes = new();

    [Fact]
    public async Task HandleAsync_PageWithVideos_ReportsStepsAndRemembersThePage()
    {
        // Arrange
        _fetcher.Pages["https://site/a"] = Fixtures.Read("pages", "dailymotion_article.html");
        _prefixes.Add("dailymotion", "https://players.example-cdn.com/");

        // Act
        var found = await Handler().HandleAsync("dailymotion", "https://site/a", CancellationToken.None);

        // Assert
        found.Count.ShouldBe(5);
        _reporter.Texts("step").ShouldBe(["Fetching https://site/a", "Looking for 7 kinds of Dailymotion links"]);
        _repository.RefererFor("https://www.dailymotion.com/video/x8abc12").ShouldBe("https://site/a");
    }

    [Fact]
    public async Task HandleAsync_NoVideos_ThrowsWithJavaScriptHint()
    {
        _fetcher.Pages["https://site/empty"] = Fixtures.Read("pages", "no_videos.html");

        var error = await Should.ThrowAsync<NoVideosFoundException>(() => Handler().HandleAsync("youtube", "https://site/empty", CancellationToken.None));

        error.Hint.ShouldContain("JavaScript");
    }

    [Fact]
    public async Task HandleAsync_PageCannotBeFetched_ThrowsWithTheFetchError()
    {
        await Should.ThrowAsync<OuttaException>(() => Handler().HandleAsync("youtube", "https://missing", CancellationToken.None));
    }

    private ScrapeHandler Handler() => new(new ScraperRegistry(), _fetcher, _prefixes, _repository, _reporter);

    private sealed class InMemoryPrefixStore : IPrefixStore
    {
        private readonly Dictionary<string, List<string>> _data = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, IReadOnlyList<string>> All() => _data.ToDictionary(e => e.Key, e => (IReadOnlyList<string>)e.Value);

        public IReadOnlyList<string> ForSite(string site) => _data.GetValueOrDefault(site) ?? [];

        public void Add(string site, string prefix) => (_data.TryGetValue(site, out var list) ? list : _data[site] = []).Add(prefix);

        public void Remove(string site, string prefix) => _data[site].Remove(prefix);
    }
}
