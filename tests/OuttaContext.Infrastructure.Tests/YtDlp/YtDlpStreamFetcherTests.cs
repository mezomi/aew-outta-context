using System.Text.Json;
using OuttaContext.Core.Media.Hls;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.Infrastructure.YtDlp;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.YtDlp;

public sealed class YtDlpStreamFetcherTests : IDisposable
{
    private static readonly Dictionary<string, string> Headers = new() { ["Referer"] = "https://geo.dailymotion.com/" };

    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly List<string> _details = [];

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task DownloadPiecesAsync_Pieces_WritesAJobAndPosesAsChrome()
    {
        // Arrange
        string? job = null;
        _runner.Respond = spec => new FakeRun
        {
            OutputLines = ["OUTTA-PIECE 1", "OUTTA-PIECE 2", "noise"],
            SideEffect = s =>
            {
                job = File.ReadAllText(FakeRun.ValueAfter(s, "--load-info-json"));
                File.WriteAllText(FakeRun.ValueAfter(s, "--output"), "joined");
            },
        };
        var done = new List<int>();
        StreamPiece[] pieces = [new("https://cdn/init.mp4"), new("https://cdn/seg-1.m4s", 6, new ByteRange(100, 199))];

        // Act
        var result = await Fetcher().DownloadPiecesAsync(pieces, Headers, _temp.File("stream0.mp4"), done.Add, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeTrue();
        File.ReadAllText(_temp.File("stream0.mp4")).ShouldBe("joined");
        FakeRun.ValueAfter(_runner.Runs[0], "--impersonate").ShouldBe("chrome");
        done.ShouldBe([1, 2]);
        using JsonDocument parsed = JsonDocument.Parse(job!);
        parsed.RootElement.GetProperty("protocol").GetString().ShouldBe("http_dash_segments");
        JsonElement second = parsed.RootElement.GetProperty("fragments")[1];
        second.GetProperty("byte_range").GetProperty("end").GetInt64().ShouldBe(200); // yt-dlp's end is exclusive
        File.Exists(_temp.File("stream0.mp4.job.json")).ShouldBeFalse();
    }

    [Fact]
    public async Task DownloadPiecesAsync_ChromeRefused_TriesFirefoxThenRemembersIt()
    {
        _runner.Respond = spec => spec.Arguments.Contains("chrome")
            ? FakeRun.Failing("ERROR: HTTP Error 403: Forbidden")
            : new FakeRun { SideEffect = s => File.WriteAllText(FakeRun.ValueAfter(s, "--output"), "ok") };
        YtDlpStreamFetcher fetcher = Fetcher();

        await fetcher.DownloadPiecesAsync([new("https://cdn/a.ts", 4)], Headers, _temp.File("a.mp4"), _ => { }, CancellationToken.None);
        await fetcher.DownloadPiecesAsync([new("https://cdn/b.ts", 4)], Headers, _temp.File("b.mp4"), _ => { }, CancellationToken.None);

        Impersonations().ShouldBe(["chrome", "firefox", "firefox"]);
        _details.ShouldContain("Stream requests work as: firefox");
    }

    [Fact]
    public async Task DownloadPiecesAsync_EveryWayRefused_ExplainsEachAttempt()
    {
        _runner.Respond = _ => FakeRun.Failing("ERROR: HTTP Error 403: Forbidden");

        var result = await Fetcher().DownloadPiecesAsync([new("https://cdn/a.ts", 4)], Headers, _temp.File("a.mp4"), _ => { }, CancellationToken.None);

        result.Error.Message.ShouldContain("chrome: HTTP Error 403");
        result.Error.Message.ShouldContain("plain: HTTP Error 403");
        result.Error.Hint.ShouldContain("--cookies-from-browser");
        Impersonations().ShouldBe(["chrome", "firefox", null]);
    }

    [Fact]
    public async Task DownloadTextAsync_Playlist_ReturnsItsText()
    {
        _runner.Respond = _ => new FakeRun { SideEffect = s => File.WriteAllText(FakeRun.ValueAfter(s, "--output"), "#EXTM3U") };

        var text = await Fetcher().DownloadTextAsync("https://cdn/m.m3u8", Headers, _temp.File("s.m3u8"), CancellationToken.None);

        text.Value.ShouldBe("#EXTM3U");
    }

    private YtDlpStreamFetcher Fetcher() =>
        new(_runner, InstalledTools.Only(_temp, "yt-dlp"), new YtDlpOptions(), _details.Add);

    private List<string?> Impersonations() =>
        _runner.Runs.Select(run => run.Arguments.Contains("--impersonate") ? FakeRun.ValueAfter(run, "--impersonate") : null).ToList();
}
