using OuttaContext.Core.Clips;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Uploads;
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
        MakeClipResult result = await _builder.Build().HandleAsync(request, CancellationToken.None);

        // Assert
        Clip clip = result.Clip;
        clip.Title.ShouldBe("Table spot");
        clip.FilePath.ShouldBe(Path.Combine(_builder.Settings.ClipFolder, "Table spot.mp4"));
        File.Exists(clip.FilePath).ShouldBeTrue();
        (clip.Source, clip.SourceUrl).ShouldBe(("youtube", "https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        (clip.Start, clip.End, clip.Duration).ShouldBe((45, 65, 20));
        _builder.Resolver.Requests[0].MaxQuality.ShouldBe(1080);
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
    public async Task HandleAsync_BlankTitle_ThrowsInvalidTitle()
    {
        await Should.ThrowAsync<InvalidTitleException>(() => Run(title: "   "));
    }

    [Fact]
    public async Task HandleAsync_DuplicateTitle_IsRefusedBeforeAnyDownload()
    {
        await Run(title: "Same");

        await Should.ThrowAsync<DuplicateTitleException>(() => Run(title: "same", range: "5-10"));

        _builder.Downloader.Downloads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_FileAlreadyOnDisk_IsRefused()
    {
        _temp.Write(Path.Combine("clips", "On disk.mp4"));

        await Should.ThrowAsync<DuplicateTitleException>(() => Run(title: "On disk"));
    }

    [Fact]
    public async Task HandleAsync_RangeLongerThanCap_ThrowsClipTooLongWithoutNetwork()
    {
        var error = await Should.ThrowAsync<ClipTooLongException>(() => Run(range: "1:30-11:52"));

        error.Message.ShouldContain("over the 45s clip cap");
        error.Hint.ShouldContain("max-clip-length");
        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(120.0, "1:00-2:30")]
    [InlineData(null, "0:00-5:00")]
    public async Task HandleAsync_RaisedOrDisabledCap_AllowsLongerClips(double? cap, string range)
    {
        _builder.Settings = _builder.Settings with { MaxClipLength = cap };

        MakeClipResult result = await Run(range: range);

        result.Clip.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleAsync_EndBeforeStart_ThrowsWithoutNetwork()
    {
        await Should.ThrowAsync<InvalidTimeRangeException>(() => Run(range: "1:52-1:30"));

        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("0:20-0:40")]
    [InlineData("0:31-0:35")]
    public async Task HandleAsync_RangePastTheVideoEnd_ThrowsBeforeDownloading(string range)
    {
        _builder.Resolver.Duration = 30;

        await Should.ThrowAsync<RangeOutsideVideoException>(() => Run(range: range));

        _builder.Downloader.Downloads.ShouldBeEmpty();
        _builder.Repository.ListClips().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UnknownVideoLength_OnlyWarns()
    {
        _builder.Resolver.Duration = null;

        await Run(range: "0-5");

        _builder.Reporter.Texts("warn").ShouldContain(w => w.Contains("did not report", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_UnsupportedUrl_ThrowsWithoutNetwork()
    {
        await Should.ThrowAsync<UnsupportedUrlException>(() => Run(url: "https://vimeo.com/1"));

        _builder.Resolver.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DownloadFails_SavesNothing()
    {
        _builder.Downloader.Failure = new ToolError("yt-dlp", "nope", "hint");

        await Should.ThrowAsync<ToolFailedException>(() => Run());

        _builder.Repository.ListClips().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DailymotionWithPage_AsksAsTheEmbeddedPlayerFirst()
    {
        await Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl);

        var first = _builder.Resolver.Requests[0].Request;
        first.Headers["Referer"].ShouldBe("https://geo.dailymotion.com/");
        first.ExtractorQuery!["embedder"].ShouldBe(PageUrl);
    }

    [Fact]
    public async Task HandleAsync_FirstWayRefused_TriesTheNextWay()
    {
        _builder.Resolver.Refusals = 1;

        await Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl);

        _builder.Resolver.Requests[1].Request.Label.ShouldBe("direct, page as referer");
        _builder.Reporter.Texts("step").ShouldContain("Retrying as direct, page as referer...");
    }

    [Fact]
    public async Task HandleAsync_EveryWayRefused_ThrowsTheLastError()
    {
        _builder.Resolver.Refusals = 3;

        var error = await Should.ThrowAsync<ToolFailedException>(() => Run(url: GeoPlayerUrl, range: "0-5", referer: PageUrl));

        error.Message.ShouldContain("403");
        _builder.Resolver.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_LinkFoundByScrape_UsesThePageAsReferer()
    {
        _builder.Repository.RememberLinks([("https://www.dailymotion.com/video/x8abc12", PageUrl, "embed")]);

        MakeClipResult result = await Run(url: "https://dai.ly/x8abc12", range: "0-5");

        result.Clip.PageUrl.ShouldBe(PageUrl);
    }

    [Fact]
    public async Task HandleAsync_NoEpisode_ClipIsNotAdded()
    {
        MakeClipResult result = await Run(noEpisode: true);

        result.Status.ShouldBeNull();
        result.Clip.EpisodeId.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_ReachingTheTarget_SaysReadyToBuild()
    {
        _builder.Settings = _builder.Settings with { MaxClipLength = null, EpisodeLength = 30 };
        _builder.Media.Durations["a.mp4"] = 19.9;
        _builder.Media.Durations["b.mp4"] = 15;
        await Run(title: "a", range: "0-20");

        MakeClipResult result = await Run(title: "b", range: "0-15");

        result.Clip.Duration.ShouldBe(15);
        result.Status!.IsReady.ShouldBeTrue();
        _builder.Reporter.Texts("warn").ShouldContain(w => w.Contains("outta episode build", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_NoFfprobe_UsesTheRequestedLength()
    {
        _builder.Media.IsAvailable = false;

        MakeClipResult result = await Run(range: "0-12");

        result.Clip.Duration.ShouldBe(12);
    }

    [Fact]
    public async Task HandleAsync_Upload_SendsTheClipToTheStorage()
    {
        var uploader = new RecordingUploader();
        _builder.Uploader = uploader;

        MakeClipResult result = await Run(upload: true, storage: "drive");

        uploader.Uploaded.ShouldBe([(result.Clip.Id, (string?)"drive")]);
    }

    private Task<MakeClipResult> Run(
        string url = YouTubeUrl,
        string range = "0:45-1:05",
        string title = "Table spot",
        bool noEpisode = false,
        bool upload = false,
        string? storage = null,
        string? referer = null) =>
        _builder.Build().HandleAsync(new MakeClipRequest(url, range, title, null, noEpisode, upload, storage, referer), CancellationToken.None);

    private sealed class RecordingUploader : IClipUploader
    {
        public List<(long ClipId, string? Storage)> Uploaded { get; } = [];

        public Task<string> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken, string subfolder = "")
        {
            Uploaded.Add((clip.Id, storage));
            return Task.FromResult("remote");
        }
    }
}
