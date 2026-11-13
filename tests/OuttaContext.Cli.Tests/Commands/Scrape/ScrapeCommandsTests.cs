using System.Text.Json;

namespace OuttaContext.Cli.Tests.Commands.Scrape;

public sealed class ScrapeCommandsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task ScrapeDailymotion_Article_ListsEveryVideoWithItsKind()
    {
        (int code, string output) = await _cli.RunAsync("scrape", "dailymotion", "https://news/a");

        code.ShouldBe(0, output);
        output.ShouldContain("Found 5 videos");
        output.ShouldContain("https://www.dailymotion.com/video/k1ojSwOaH7psOtMbpGS");
        output.ShouldContain("(geo player embed)");
    }

    [Fact]
    public async Task ScrapeJson_Article_PrintsOnlyJson()
    {
        (_, string output) = await _cli.RunAsync("scrape", "dailymotion", "https://news/a", "--json");

        using JsonDocument json = JsonDocument.Parse(output);
        json.RootElement[0].GetProperty("page_url").GetString().ShouldBe("https://news/a");
    }

    [Fact]
    public async Task ScrapeYouTube_PageWithoutVideos_ExplainsWhy()
    {
        (int code, string output) = await _cli.RunAsync("scrape", "youtube", "https://news/empty");

        code.ShouldBe(1);
        output.ShouldContain("JavaScript");
    }

    [Fact]
    public async Task Prefixes_AddListRemove_RoundTrip()
    {
        (await _cli.RunAsync("scrape", "prefixes", "add", "dailymotion", "https://players.example-cdn.com/")).ExitCode.ShouldBe(0);
        (int duplicate, _) = await _cli.RunAsync("scrape", "prefixes", "add", "dailymotion", "https://players.example-cdn.com/");
        (_, string listed) = await _cli.RunAsync("scrape", "prefixes", "list", "dailymotion");
        (int removed, _) = await _cli.RunAsync("scrape", "prefixes", "remove", "dailymotion", "https://players.example-cdn.com/");

        duplicate.ShouldBe(1);
        listed.ShouldContain("https://players.example-cdn.com/");
        listed.ShouldContain("geo player embed");
        removed.ShouldBe(0);
    }

    [Fact]
    public async Task PrefixesAdd_UnknownSite_ListsTheSites()
    {
        (int code, string output) = await _cli.RunAsync("scrape", "prefixes", "add", "vimeo", "https://x/");

        code.ShouldBe(1);
        output.ShouldContain("Sites you can use");
    }
}
