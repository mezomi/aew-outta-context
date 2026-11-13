using OuttaContext.Core.Errors;
using OuttaContext.Core.Scraping;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Core.Tests.Scraping;

public sealed class ScraperTests
{
    private const string Page = "https://news/article";
    private readonly ScraperRegistry _scrapers = new();

    [Fact]
    public void Extract_DailymotionArticle_FindsEveryEmbedInPageOrder()
    {
        string html = Fixtures.Read("pages", "dailymotion_article.html");

        IReadOnlyList<FoundVideo> found = _scrapers.Get("dailymotion").Extract(html, Page, []);

        found.Select(v => v.VideoId).ShouldBe(["k1ojSwOaH7psOtMbpGS", "x8abc12", "x7short", "x6script", "x5json"]);
        found[0].Kind.ShouldBe("geo player embed");
        found[0].Url.ShouldBe("https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS");
        found.ShouldAllBe(v => v.PageUrl == Page);
    }

    [Fact]
    public void Extract_YouTubeArticle_RemovesDuplicatesAndSkipsPlaylists()
    {
        string html = Fixtures.Read("pages", "youtube_article.html");

        IReadOnlyList<FoundVideo> found = _scrapers.Get("youtube").Extract(html, Page, []);

        found.Select(v => v.VideoId).ShouldBe(["dQw4w9WgXcQ", "9bZkp7q19f0", "kJQP7kiw5Fk", "aqz-KE-bpKQ"]);
    }

    [Fact]
    public void Extract_DataVideoAttribute_NeedsDailymotionOnThePage()
    {
        _scrapers.Get("dailymotion").Extract("<div data-video=\"x6script\"></div>", Page, []).ShouldBeEmpty();
    }

    [Fact]
    public void Extract_RegisteredPrefix_MatchesLinksThatStartWithIt()
    {
        string html = Fixtures.Read("pages", "custom_embed.html");
        Scraper scraper = _scrapers.Get("dailymotion");

        var withoutPrefix = scraper.Extract(html, Page, []);
        var withPrefix = scraper.Extract(html, Page, ["https://players.example-cdn.com/"]);

        withoutPrefix.ShouldBeEmpty();
        withPrefix.Select(v => v.VideoId).ShouldBe(["x4custom1", "x4custom2"]);
        withPrefix.ShouldAllBe(v => v.Kind == "registered prefix");
    }

    [Theory]
    [InlineData("https://x.com/player.html?video=abc12", "abc12")]
    [InlineData("https://x.com/w?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://x.com/v/abc123.html", "abc123")]
    [InlineData("//x.com/e/abc123/", "abc123")]
    [InlineData("https://x.com/", null)]
    [InlineData("https://x.com/ab", null)]
    public void IdFromLink_Link_ReturnsQueryValueOrLastPathPart(string link, string? expected)
    {
        Scraper.IdFromLink(link).ShouldBe(expected);
    }

    [Fact]
    public void Get_UnknownSite_ListsTheSitesYouCanUse()
    {
        Should.Throw<NotFoundException>(() => _scrapers.Get("vimeo")).Hint.ShouldContain("dailymotion");
    }
}
