using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Sources;
using OuttaContext.IntegrationTests.Support;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.IntegrationTests.Media;

[Trait("Category", "Integration")]
public sealed class ClipDownloadTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RecordingReporter _reporter = new();
    private readonly RealTools _real;

    public ClipDownloadTests()
    {
        _real = new RealTools(_temp, _reporter);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("mpegts")]
    [InlineData("fmp4")]
    public async Task DownloadAsync_HlsStream_FetchesOnlyNeededPiecesAndCutsExactly(string segmentType)
    {
        // Arrange: a 60s stream in 4s pieces, served like a CDN
        TestVideos.RequireFfmpegAndYtDlp();
        using var server = new StaticFileServer(_temp.File("www"));
        string source = TestVideos.Make(_temp.File("source.mp4"), 60);
        string playlist = TestVideos.MakeHls(source, _temp.File("www", segmentType), segmentType);
        var request = new RequestVariant("direct", server.UrlFor(playlist), RequestHeaders.None);
        string output = _temp.File("clips", "Steve the Clown.mp4");

        // Act
        ResolvedVideo video = (await _real.Resolver.ResolveAsync(request, Values.Quality(1080), Ct)).ShouldSucceed();
        DownloadedClip clip = (await _real.Downloader.DownloadAsync(video, output, Values.Span("45.5-53.5"), Ct)).ShouldSucceed();

        // Assert
        video.IsHls.ShouldBeTrue();
        (await _real.Ffmpeg.ProbeAsync(clip.Path, Ct)).ShouldSucceed().Duration.Seconds.ShouldBe(8, 0.3);
        RecordedProgress pieces = _reporter.ProgressBars.Single(p => p.Unit == ProgressUnit.Segments);
        pieces.Total!.Value.ShouldBeLessThanOrEqualTo(4); // 3 pieces + margin, not all 15
        _reporter.ProgressBars.Single(p => p.Unit == ProgressUnit.Seconds).Updates[^1].ShouldBe((8, 8));
    }

    [Fact]
    public async Task DownloadAsync_ProgressiveMp4_IsCutDirectlyFromTheServer()
    {
        // Arrange
        TestVideos.RequireFfmpegAndYtDlp();
        using var server = new StaticFileServer(_temp.File("www"));
        string source = TestVideos.Make(_temp.File("www", "source.mp4"), 20);
        var request = new RequestVariant("direct", server.UrlFor(source), RequestHeaders.None);

        // Act
        ResolvedVideo video = (await _real.Resolver.ResolveAsync(request, Values.Quality(1080), Ct)).ShouldSucceed();
        DownloadedClip clip = (await _real.Downloader.DownloadAsync(video, _temp.File("out", "Table spot.mp4"), Values.Span("5-9.5"), Ct)).ShouldSucceed();

        // Assert
        video.IsHls.ShouldBeFalse();
        (await _real.Ffmpeg.ProbeAsync(clip.Path, Ct)).ShouldSucceed().Duration.Seconds.ShouldBe(4.5, 0.25);
    }
}
