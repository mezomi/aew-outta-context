using OuttaContext.Core.Scraping;
using OuttaContext.TestKit.Assertions;
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
        _prefixes.Add("dailymotion", LinkPrefix.Create("https://players.example-cdn.com/").ShouldSucceed());

        // Act
        var result = await Handler().HandleAsync("dailymotion", "https://site/a", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldSucceed().Count.ShouldBe(5);
        _reporter.Texts("step").ShouldBe(["Fetching https://site/a", "Looking for 7 kinds of Dailymotion links"]);
        _repository.RefererFor("https://www.dailymotion.com/video/x8abc12").ShouldHaveValue().ShouldBe("https://site/a");
    }

    [Fact]
    public async Task HandleAsync_NoVideos_FailsWithJavaScriptHint()
    {
        _fetcher.Pages["https://site/empty"] = Fixtures.Read("pages", "no_videos.html");

        var result = await Handler().HandleAsync("youtube", "https://site/empty", TestContext.Current.CancellationToken);

        result.ShouldFailWith("scrape.none_found").Hint.ShouldContain("JavaScript");
    }

    [Fact]
    public async Task HandleAsync_PageCannotBeFetched_ReturnsTheFetchError()
    {
        var result = await Handler().HandleAsync("youtube", "https://missing", TestContext.Current.CancellationToken);

        result.ShouldFailWith("web.http_status");
    }

    [Fact]
    public async Task HandleAsync_UnknownSite_FailsWithoutFetching()
    {
        var result = await Handler().HandleAsync("vimeo", "https://site/a", TestContext.Current.CancellationToken);

        result.ShouldFailWith("scrape.unknown_site");
        _fetcher.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NotAWebAddress_FailsWithoutFetching()
    {
        var result = await Handler().HandleAsync("youtube", "site/a", TestContext.Current.CancellationToken);

        result.ShouldFailWith("url.invalid");
        _fetcher.Requests.ShouldBeEmpty();
    }

    private ScrapeHandler Handler() => new(ScraperRegistry.Default, _fetcher, _prefixes, _repository, _reporter);
}
