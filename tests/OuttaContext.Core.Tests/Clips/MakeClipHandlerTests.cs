using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Time;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Core.Tests.Clips;

public sealed class MakeClipHandlerTests : IDisposable
{
    private const string YouTubeUrl = "https://youtu.be/dQw4w9WgXcQ";
    private const string GeoPlayerUrl = "https://geo.dailymotion.com/player/xc394.html?video=k1ojSwOaH7psOtMbpGS";
    private const string PageUrl = "https://news/page";

    private readonly TempDirectory _temp = new();
    private readonly MakeClipHandlerBuilder _builder;

    public MakeClipHandlerTests()
    {
        _builder = new MakeClipHandlerBuilder(_temp);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task HandleAsync_ValidRequest_SavesTitledClipAndAddsItToTheEpisode()
    {
        // Arrange
        var request = new MakeClipRequest(YouTubeUrl, "0:45-1:05", "Table spot");

        // Act
        Result<MakeClipResult, Error> result = await _builder.Build().HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Clip clip = result.ShouldSucceed().Clip;
        clip.Title.Value.ShouldBe("Table spot");
        clip.FilePath.ShouldBe(Path.Combine(_builder.Settings.ClipFolder, "Table spot.mp4"));
        File.Exists(clip.FilePath).ShouldBeTrue();
        (clip.Source, clip.SourceUrl).ShouldBe(("youtube", "https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        (clip.Span.Start.Seconds, clip.Span.End.Seconds, clip.Duration.Seconds).ShouldBe((45, 65, 20));
        _builder.Resolver.Requests[0].MaxQuality.Height.ShouldBe(1080);
        _builder.Reporter.Texts("step").Take(4).ShouldBe(
        [
            "Detected source: YouTube",
            "Reading video info...",
            "Video: \"AEW Dynamite highlights\" (10:00)",
            "Clipping 0:45-1:05 (20s, best up to 1080p)",
        ]);
        _builder.Reporter.Texts("success")[^1].ShouldBe("Added to \"Episode 1\": 0:20 of 10:00 (9:40 left)");
        _builder.Reporter.ProgressBars.ShouldHaveSingleItem().Unit.ShouldBe(ProgressUnit.Seconds);
    }

    [Fact]
    public async Task HandleAsync_BlankTitle_FailsWithTitleEmpty()
    {
        (await Run(title: "   ")).ShouldFailWith("clip.title_empty");
    }

    [Fact]
    public async Task HandleAsync_DuplicateTitle_IsRefusedBeforeAnyDownload()
    {
        (await Run(title: "Same")).ShouldSucceed();

        (await Run(title: "same", range: "5-10")).ShouldFailWith("clip.title_taken");

        _builder.Downloader.Downloads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_FileAlreadyOnDisk_IsRefused()
    {
        _temp.Write(Path.Combine("clips", "On disk.mp4"));

        (await Run(title: "On disk")).ShouldFailWith("clip.file_exists");
    }

    [Fact]
    public async Task HandleAsync_RangeLongerThanCap_FailsWithClipTooLongWithoutNetwork()
    {
        Error error = (await Run(range: "1:30-11:52")).ShouldFailWith("clip.too_long");

        error.Message.ShouldContain("over the 45s clip cap");
        error.Hint.ShouldContain("max-clip-length");
        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(120.0, "1:00-2:30")]
    [InlineData(null, "0:00-5:00")]
    public async Task HandleAsync_RaisedOrDisabledCap_AllowsLongerClips(double? cap, string range)
    {
        _builder.Settings = _builder.Settings with { MaxClipLength = cap is { } seconds ? Values.Time(seconds) : Maybe<VideoTime>.None };

        (await Run(range: range)).ShouldSucceed();
    }

    [Fact]
    public async Task HandleAsync_EndBeforeStart_FailsWithoutNetwork()
    {
        (await Run(range: "1:52-1:30")).ShouldFailWith("range.end_before_start");

        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("0:20-0:40")]
    [InlineData("0:31-0:35")]
    public async Task HandleAsync_RangePastTheVideoEnd_FailsBeforeDownloading(string range)
    {
        _builder.Resolver.Duration = 30;

        (await Run(range: range)).ShouldFailWith("range.outside_video");

        _builder.Downloader.Downloads.ShouldBeEmpty();
        _builder.Repository.ListClips().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnknownVideoLength_OnlyWarns()
    {
        _builder.Resolver.Duration = null;

        (await Run(range: "0-5")).ShouldSucceed();

        _builder.Reporter.Texts("warn").ShouldContain(w => w.Contains("did not report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_UnsupportedUrl_FailsWithoutNetwork()
    {
        (await Run(url: "https://vimeo.com/1")).ShouldFailWith("source.unsupported");

        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_BuiltEpisode_FailsWithoutNetwork()
    {
        _builder.Episodes.Finish(_builder.Episodes.Current(), "/x.mp4");

        (await Run(episode: "Episode 1")).ShouldFailWith("episode.built");

        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DownloadFails_ReturnsItsErrorAndSavesNothing()
    {
        _builder.Downloader.Failure = new Error("yt-dlp.network", "Network problem: nope", "Check your connection");

        (await Run()).ShouldFailWith("yt-dlp.network");

        _builder.Repository.ListClips().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DailymotionWithPage_AsksAsTheEmbeddedPlayerFirst()
    {
        (await Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl)).ShouldSucceed();

        var first = _builder.Resolver.Requests[0].Request;
        first.Headers.Values["Referer"].ShouldBe("https://geo.dailymotion.com/");
        first.ExtractorQuery["embedder"].ShouldBe(PageUrl);
    }

    [Fact]
    public async Task HandleAsync_FirstWayRefused_TriesTheNextWay()
    {
        _builder.Resolver.Refusals = 1;

        (await Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl)).ShouldSucceed();

        _builder.Resolver.Requests[1].Request.Label.ShouldBe("direct, page as referer");
        _builder.Reporter.Texts("step").ShouldContain("Retrying as direct, page as referer...");
    }

    [Fact]
    public async Task HandleAsync_EveryWayRefused_ReturnsTheLastError()
    {
        _builder.Resolver.Refusals = 3;

        Error error = (await Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl)).ShouldFailWith("yt-dlp.forbidden");

        error.Message.ShouldContain("403");
        _builder.Resolver.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_LinkFoundByScrape_UsesThePageAsReferer()
    {
        _builder.Repository.RememberLinks([new ScrapedLink("https://www.dailymotion.com/video/x8abc12", PageUrl, "embed")]);

        MakeClipResult result = (await Run(url: "https://dai.ly/x8abc12", range: "0-5")).ShouldSucceed();

        result.Clip.PageUrl.ShouldHaveValue().ShouldBe(PageUrl);
    }

    [Fact]
    public async Task HandleAsync_NoEpisode_ClipIsNotAdded()
    {
        MakeClipResult result = (await Run(noEpisode: true)).ShouldSucceed();

        result.Status.HasNoValue.ShouldBeTrue();
        result.Clip.EpisodeId.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ReachingTheTarget_SaysReadyToBuild()
    {
        _builder.Settings = _builder.Settings with { MaxClipLength = Maybe<VideoTime>.None, EpisodeLength = Values.Time(30) };
        _builder.Media.Durations["a.mp4"] = 19.9;
        _builder.Media.Durations["b.mp4"] = 15;
        (await Run(title: "a", range: "0-20")).ShouldSucceed();

        MakeClipResult result = (await Run(title: "b", range: "0-15")).ShouldSucceed();

        result.Clip.Duration.Seconds.ShouldBe(15);
        result.Status.ShouldHaveValue().IsReady.ShouldBeTrue();
        _builder.Reporter.Texts("warn").ShouldContain(w => w.Contains("outta episode build", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_NoFfprobe_UsesTheRequestedLength()
    {
        _builder.Media.IsAvailable = false;

        MakeClipResult result = (await Run(range: "0-12")).ShouldSucceed();

        result.Clip.Duration.Seconds.ShouldBe(12);
    }

    [Fact]
    public async Task HandleAsync_Upload_SendsTheClipToTheStorage()
    {
        MakeClipResult result = (await Run(upload: true, storage: "drive")).ShouldSucceed();

        _builder.Uploader.Uploads.Select(u => (u.Clip.Id, u.Storage)).ShouldBe([(result.Clip.Id, (string?)"drive")]);
    }

    [Fact]
    public async Task HandleAsync_UploadFails_ReturnsTheErrorButKeepsTheClip()
    {
        _builder.Uploader.Failure = new Error("storage.no_default", "No storage", "Add one");

        (await Run(upload: true)).ShouldFailWith("storage.no_default");

        _builder.Repository.ListClips().ShouldHaveSingleItem();
    }

    private Task<Result<MakeClipResult, Error>> Run(
        string url = YouTubeUrl,
        string range = "0:45-1:05",
        string title = "Table spot",
        bool noEpisode = false,
        bool upload = false,
        string? storage = null,
        string? referer = null,
        string? episode = null) =>
        _builder.Build().HandleAsync(
            new MakeClipRequest(url, range, title, episode, noEpisode, upload, storage, referer), TestContext.Current.CancellationToken);
}
