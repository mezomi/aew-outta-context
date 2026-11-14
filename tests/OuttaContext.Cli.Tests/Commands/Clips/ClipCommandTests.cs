using System.Text.Json;

namespace OuttaContext.Cli.Tests.Commands.Clips;

public sealed class ClipCommandTests : IDisposable
{
    private const string YouTubeUrl = "https://youtu.be/dQw4w9WgXcQ";
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task Clip_TitleAndRange_SavesClipAndPrintsEpisodeTotals()
    {
        await _cli.WithClipFolderAsync();

        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "--range", "0:45-1:05", "--title", "Table spot");

        code.ShouldBe(0, output);
        output.ShouldContain("Detected source: YouTube");
        output.ShouldContain("Table spot.mp4");
        output.ShouldContain("Added to \"Episode 1\": 0:20 of 10:00 (9:40 left)");
    }

    [Theory]
    [InlineData("--range", "0-5")]
    [InlineData("--title", "x")]
    public async Task Clip_MissingTitleOrRange_IsAUsageError(string option, string value)
    {
        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, option, value);

        code.ShouldBe(2);
        output.ShouldContain("Missing --");
    }

    [Fact]
    public async Task Clip_ReplaceSameTitle_ReplacesTheClip()
    {
        await _cli.WithClipFolderAsync();
        (await _cli.RunAsync("clip", YouTubeUrl, "-r", "0:21-0:29", "-t", "Steve")).ExitCode.ShouldBe(0);

        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0:10-0:13", "-t", "Steve", "--replace");

        code.ShouldBe(0, output);
        output.ShouldContain("Replaced clip 1 (\"Steve\", 0:21-0:29)");
        output.ShouldContain("Still at position 1 of \"Episode 1\"");
    }

    [Fact]
    public async Task Clip_ReplaceById_GivesTheClipTheNewTitle()
    {
        await _cli.WithClipFolderAsync();
        (await _cli.RunAsync("clip", YouTubeUrl, "-r", "0:21-0:29", "-t", "Steve")).ExitCode.ShouldBe(0);

        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0:10-0:13", "-t", "Steve fixed", "--replace", "1");

        code.ShouldBe(0, output);
        (await _cli.RunAsync("episode", "status")).Output.ShouldContain("Steve fixed");
    }

    [Theory]
    [InlineData("--no-episode")]
    [InlineData("--episode", "Episode 1")]
    public async Task Clip_ReplaceWithEpisodeOption_IsAUsageError(params string[] extra)
    {
        (int code, string output) = await _cli.RunAsync(["clip", YouTubeUrl, "-r", "0-5", "-t", "x", "--replace", .. extra]);

        code.ShouldBe(2);
        output.ShouldContain("--replace keeps the clip in its current episode");
    }

    [Fact]
    public async Task Clip_UnknownCookieBrowser_IsAUsageErrorBeforeAnyDownload()
    {
        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0-5", "-t", "x", "--cookies-from-browser", "lynx");

        code.ShouldBe(2);
        output.ShouldContain("not a supported browser");
        _cli.Resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clip_SiteRefuses_VerboseShowsYtDlpsOwnOutput()
    {
        await _cli.WithClipFolderAsync();
        _cli.Downloader.Failure = new Core.Common.Error("yt-dlp.network", "Network problem: timed out", "Check your connection.", "ERROR: [generic] timed out after 20s");

        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0-5", "-t", "x", "-v");

        code.ShouldBe(1);
        output.ShouldContain("error: Network problem: timed out");
        output.ShouldContain("hint: Check your connection.");
        output.ShouldContain("ERROR: [generic] timed out after 20s");
    }

    [Fact]
    public async Task Clip_OverTheCap_PrintsErrorAndHint()
    {
        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "--range", "1:30-11:52", "--title", "typo");

        code.ShouldBe(1);
        output.ShouldContain("error: 1:30-11:52 is 10:22 long, over the 45s clip cap.");
        output.ShouldContain("hint:");
    }

    [Fact]
    public async Task Clip_EpisodeAndNoEpisode_IsAUsageError()
    {
        (int code, _) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0-5", "-t", "x", "--episode", "a", "--no-episode");

        code.ShouldBe(2);
    }

    [Fact]
    public async Task Clip_FileAlreadyOnDisk_PrintsErrorWithoutAStackTrace()
    {
        _cli.Downloader.Failure = null;
        await _cli.WithClipFolderAsync();
        File.WriteAllText(Path.Combine(_cli.ClipFolder, "x.mp4"), "already there");

        (int code, string output) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0-5", "-t", "x");

        code.ShouldBe(1);
        output.ShouldContain("already exists");
        output.ShouldNotContain("   at ");
    }

    [Fact]
    public async Task Clip_CookiesOption_ReachesYtDlpOptions()
    {
        await _cli.WithClipFolderAsync();

        (int code, _) = await _cli.RunAsync("clip", YouTubeUrl, "-r", "0-5", "-t", "x", "--cookies-from-browser", "firefox");

        code.ShouldBe(0);
    }

    [Fact]
    public async Task EpisodeStatusJson_AfterClip_ListsTheClip()
    {
        await _cli.WithClipFolderAsync();
        await _cli.RunAsync("clip", YouTubeUrl, "-r", "0:45-1:05", "-t", "Table spot");

        (_, string output) = await _cli.RunAsync("episode", "status", "--json");

        using JsonDocument json = JsonDocument.Parse(output);
        json.RootElement.GetProperty("clips")[0].GetProperty("title").GetString().ShouldBe("Table spot");
    }
}
