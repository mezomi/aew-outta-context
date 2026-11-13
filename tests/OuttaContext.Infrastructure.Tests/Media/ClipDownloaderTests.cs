using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.Media;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.Media;

public sealed class ClipDownloaderTests : IDisposable
{
    private const string PlaylistUrl = "https://cdn.example/video/h264/manifest.m3u8";
    private static readonly VideoInfo Info = new("k1oj", "AEW", Values.Time(2800), "https://www.dailymotion.com/video/k1oj", "Dailymotion");
    private static readonly RequestHeaders Headers = Values.Headers(("Referer", "https://geo.dailymotion.com/"));

    private readonly TempDirectory _temp = new();
    private readonly FakeStreamFetcher _fetcher = new();
    private readonly FakeMediaProcessor _media = new();
    private readonly RecordingReporter _reporter = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task DownloadAsync_HlsStream_FetchesOnlyTheNeededPiecesThenCutsLocally()
    {
        // Arrange
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8_native", Headers)]);
        string output = _temp.File("clips", "Steve the Clown.mp4");

        // Act
        DownloadedClip clip = (await Downloader().DownloadAsync(video, output, Values.Span("8-16"), Ct)).ShouldSucceed();

        // Assert
        clip.Path.ShouldBe(output);
        _fetcher.PieceDownloads.ShouldHaveSingleItem().Pieces.Select(p => p.Url.Split('/')[^1])
            .ShouldBe(["init.mp4", "seg-1.m4s", "seg-2.m4s"]); // 8-16s (+1s margin) is in pieces 6-12 and 12-18
        _fetcher.PieceDownloads[0].Headers.ShouldBe(Headers);
        var (inputs, _, length) = _media.Cuts.ShouldHaveSingleItem();
        length.Seconds.ShouldBe(8);
        inputs[0].Offset.Seconds.ShouldBe(8 - 6);
        inputs[0].IsRemote.ShouldBeFalse();
        _reporter.ProgressBars.Select(p => p.Unit).ShouldBe([ProgressUnit.Segments, ProgressUnit.Seconds]);
        _reporter.ProgressBars.ShouldAllBe(p => p.Disposed);
        Directory.GetDirectories(_temp.File("clips")).ShouldBeEmpty(); // work folder removed
    }

    [Fact]
    public async Task DownloadAsync_SeparateVideoAndAudio_OneLocalFilePerStream()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8_native", Headers), new StreamFormat(PlaylistUrl, "m3u8_native", Headers)]);

        (await Downloader().DownloadAsync(video, _temp.File("c.mp4"), Values.Span("0-4"), Ct)).ShouldSucceed();

        _media.Cuts[0].Inputs.Count.ShouldBe(2);
        _fetcher.PieceDownloads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DownloadAsync_EncryptedStream_IsRefused()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "encrypted.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), Values.Span("0-4"), Ct);

        result.ShouldFailWith("hls.encrypted").Message.ShouldContain("encrypted");
        _media.Cuts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadAsync_RangePastTheStream_SaysHowLongItIs()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), Values.Span("100-105"), Ct);

        result.ShouldFailWith("hls.no_pieces").Message.ShouldContain("0:28");
    }

    [Fact]
    public async Task DownloadAsync_SiteRefusesPieces_ReturnsTheFetchError()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        _fetcher.PieceFailure = new Error("yt-dlp.stream_refused", "The site refused the stream: chrome: 403", "Try --cookies-from-browser firefox.");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), Values.Span("0-4"), Ct);

        result.ShouldFailWith("yt-dlp.stream_refused");
        File.Exists(_temp.File("c.mp4")).ShouldBeFalse();
    }

    [Fact]
    public async Task DownloadAsync_PlainStreams_AreCutDirectlyFromTheirUrls()
    {
        var video = new ResolvedVideo(Info, [new StreamFormat("https://v", "https", Headers), new StreamFormat("https://a", "https", Headers)]);

        (await Downloader().DownloadAsync(video, _temp.File("c.mp4"), Values.Span("45-65"), Ct)).ShouldSucceed();

        _fetcher.PieceDownloads.ShouldBeEmpty();
        _media.Cuts[0].Inputs.Select(i => (i.Source, i.Offset.Seconds)).ShouldBe([("https://v", 45.0), ("https://a", 45.0)]);
        _media.Cuts[0].Inputs[0].Headers.ShouldBe(Headers);
    }

    private ClipDownloader Downloader() => new(_fetcher, _media, _reporter);
}
