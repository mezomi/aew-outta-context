using System.Text.Json;
using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Sources;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.Infrastructure.YtDlp;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.YtDlp;

public sealed class YtDlpVideoResolverTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly RecordingReporter _reporter = new();
    private readonly YtDlpOptions _options;

    public YtDlpVideoResolverTests()
    {
        _options = new YtDlpOptions(_reporter);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task ResolveAsync_VideoWithSeparateStreams_ReturnsInfoAndBothStreams()
    {
        // Arrange
        _runner.Respond = _ => new FakeRun
        {
            OutputLines = [Json(new
        {
            id = "abc", title = "AEW", duration = 872, extractor_key = "Youtube",
            requested_formats = new object[]
            {
                new { url = "https://v", protocol = "https", http_headers = new { UA = "x" } },
                new { url = "https://a", protocol = "https" },
            },
        })]
        };

        // Act
        ResolvedVideo video = (await Resolve(new RequestVariant("direct", "https://y", Values.Headers(("Referer", "https://p"))))).ShouldSucceed();

        // Assert
        (video.Info.Title, video.Info.Duration.ShouldHaveValue().Seconds).ShouldBe(("AEW", 872.0));
        video.Streams.Select(s => s.Url).ShouldBe(["https://v", "https://a"]);
        video.Streams[0].Headers.Values["UA"].ShouldBe("x");
        video.IsHls.ShouldBeFalse();
        _runner.LastArguments.ShouldContain("--dump-single-json");
        FakeRun.ValueAfter(_runner.Runs[0], "--format").ShouldBe(YtDlpVideoResolver.FormatSelector(Values.Quality(720)));
        FakeRun.ValueAfter(_runner.Runs[0], "--add-headers").ShouldBe("Referer:https://p");
    }

    [Fact]
    public async Task ResolveAsync_SingleHlsFormat_IsHlsWithUnknownLength()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = [Json(new { id = "x", url = "https://cdn/m.m3u8", protocol = "m3u8_native" })] };

        ResolvedVideo video = (await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None))).ShouldSucceed();

        video.IsHls.ShouldBeTrue();
        video.Info.Duration.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_NegativeDuration_IsTreatedAsUnknown()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = [Json(new { id = "x", url = "https://v", duration = -1 })] };

        ResolvedVideo video = (await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None))).ShouldSucceed();

        video.Info.Duration.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_ExtractorQuery_IsSmuggledIntoTheUrl()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = [Json(new { id = "x", url = "https://v" })] };
        var query = new Dictionary<string, string> { ["embedder"] = "https://page" };

        await Resolve(new RequestVariant("embedded player", "https://www.dailymotion.com/video/x8", RequestHeaders.None, query));

        string url = _runner.LastArguments[^1];
        url.ShouldStartWith("https://www.dailymotion.com/video/x8#__youtubedl_smuggle=");
        Uri.UnescapeDataString(url.Split('=', 2)[1]).ShouldBe("{\"query\":{\"embedder\":\"https://page\"}}");
    }

    [Fact]
    public async Task ResolveAsync_YtDlpFails_ReturnsTranslatedError()
    {
        _runner.Respond = _ => FakeRun.Failing("ERROR: [dailymotion] k1: No video formats found!");

        var result = await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None));

        Error error = result.ShouldFailWith("yt-dlp.forbidden");
        error.Message.ShouldContain("Dailymotion refused");
        error.Hint.ShouldContain("--cookies-from-browser");
    }

    [Fact]
    public async Task ResolveAsync_UnreadableOutput_FailsInsteadOfThrowing()
    {
        _runner.Respond = _ => new FakeRun { OutputLines = ["not json"] };

        (await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None))).ShouldFailWith("yt-dlp.unreadable");
    }

    [Fact]
    public async Task ResolveAsync_CookiesOption_IsPassedToYtDlp()
    {
        _options.CookiesFromBrowser = Core.Settings.BrowserName.Parse("firefox").ShouldSucceed();
        _runner.Respond = _ => new FakeRun { OutputLines = [Json(new { id = "x", url = "https://v" })] };

        await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None));

        FakeRun.ValueAfter(_runner.Runs[0], "--cookies-from-browser").ShouldBe("firefox");
    }

    [Fact]
    public async Task ResolveAsync_Verbose_AsksYtDlpForDebugOutput()
    {
        _reporter.IsVerbose = true;
        _runner.Respond = _ => new FakeRun { OutputLines = [Json(new { id = "x", url = "https://v" })] };

        await Resolve(new RequestVariant("direct", "https://y", RequestHeaders.None));

        _runner.LastArguments.ShouldContain("--verbose");
    }

    [Fact]
    public async Task ResolveAsync_YtDlpNotInstalled_SaysToRunUpdate()
    {
        var resolver = new YtDlpVideoResolver(_runner, InstalledTools.Only(_temp), _options, _reporter);

        var result = await resolver.ResolveAsync(new RequestVariant("direct", "https://y", RequestHeaders.None), Values.Quality(720), TestContext.Current.CancellationToken);

        result.ShouldFailWith("yt-dlp.missing").Hint.ShouldContain("outta update");
        _runner.Runs.ShouldBeEmpty();
    }

    private Task<Result<ResolvedVideo, Error>> Resolve(RequestVariant request) =>
        new YtDlpVideoResolver(_runner, InstalledTools.Only(_temp, "yt-dlp"), _options, _reporter)
            .ResolveAsync(request, Values.Quality(720), TestContext.Current.CancellationToken);

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
