using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.Media;
using OuttaContext.Infrastructure.YtDlp;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.Media;

public sealed class ClipDownloaderTests : IDisposable
{
    private const string PlaylistUrl = "https://cdn.example/video/h264/manifest.m3u8";
    private static readonly VideoInfo Info = new("k1oj", "AEW", 2800, "https://www.dailymotion.com/video/k1oj", "Dailymotion");
    private static readonly Dictionary<string, string> Headers = new() { ["Referer"] = "https://geo.dailymotion.com/" };

    private readonly TempDirectory _temp = new();
    private readonly FakeStreamFetcher _fetcher = new();
    private readonly FakeMediaProcessor _media = new();
    private readonly RecordingReporter _reporter = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task DownloadAsync_HlsStream_FetchesOnlyTheNeededPiecesThenCutsLocally()
    {
        // Arrange
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8_native", Headers)]);
        string output = _temp.File("clips", "Steve the Clown.mp4");

        // Act
        DownloadedClip clip = (await Downloader().DownloadAsync(video, output, new TimeRange(8, 16), _reporter, CancellationToken.None)).ValueOrThrow();

        // Assert
        clip.Path.ShouldBe(output);
        _fetcher.PieceDownloads.ShouldHaveSingleItem().Pieces.Select(p => p.Url.Split('/')[^1])
            .ShouldBe(["init.mp4", "seg-1.m4s", "seg-2.m4s"]); // 8-16s (+1s margin) is in pieces 6-12 and 12-18
        _fetcher.PieceDownloads[0].Headers.ShouldBe(Headers);
        var (inputs, _, length) = _media.Cuts.ShouldHaveSingleItem();
        length.ShouldBe(8);
        inputs[0].Offset.ShouldBe(8 - 6);
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

        await Downloader().DownloadAsync(video, _temp.File("c.mp4"), new TimeRange(0, 4), _reporter, CancellationToken.None);

        _media.Cuts[0].Inputs.Count.ShouldBe(2);
        _fetcher.PieceDownloads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DownloadAsync_EncryptedStream_IsRefused()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "encrypted.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), new TimeRange(0, 4), _reporter, CancellationToken.None);

        result.Error.Message.ShouldContain("encrypted");
        _media.Cuts.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadAsync_RangePastTheStream_SaysHowLongItIs()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), new TimeRange(100, 105), _reporter, CancellationToken.None);

        result.Error.Message.ShouldContain("0:28");
    }

    [Fact]
    public async Task DownloadAsync_SiteRefusesPieces_ReturnsTheFetchError()
    {
        _fetcher.Playlists[PlaylistUrl] = Fixtures.Read("hls", "fmp4_media.m3u8");
        _fetcher.PieceFailure = new ToolError("yt-dlp", "The site refused the stream: chrome: 403", "Try --cookies-from-browser firefox.");
        var video = new ResolvedVideo(Info, [new StreamFormat(PlaylistUrl, "m3u8", Headers)]);

        var result = await Downloader().DownloadAsync(video, _temp.File("c.mp4"), new TimeRange(0, 4), _reporter, CancellationToken.None);

        result.Error.Message.ShouldContain("refused");
        File.Exists(_temp.File("c.mp4")).ShouldBeFalse();
    }

    [Fact]
    public async Task DownloadAsync_PlainStreams_AreCutDirectlyFromTheirUrls()
    {
        var video = new ResolvedVideo(Info, [new StreamFormat("https://v", "https", Headers), new StreamFormat("https://a", "https", Headers)]);

        await Downloader().DownloadAsync(video, _temp.File("c.mp4"), new TimeRange(45, 65), _reporter, CancellationToken.None);

        _fetcher.PieceDownloads.ShouldBeEmpty();
        _media.Cuts[0].Inputs.Select(i => (i.Source, i.Offset)).ShouldBe([("https://v", 45.0), ("https://a", 45.0)]);
        _media.Cuts[0].Inputs[0].Headers.ShouldBe(Headers);
    }

    private ClipDownloader Downloader() => new(_fetcher, _media);

    private sealed class FakeStreamFetcher : IStreamFetcher
    {
        public Dictionary<string, string> Playlists { get; } = new(StringComparer.Ordinal);

        public ToolError? PieceFailure { get; set; }

        public List<(IReadOnlyList<StreamPiece> Pieces, IReadOnlyDictionary<string, string> Headers)> PieceDownloads { get; } = [];

        public Task<ToolResult<string>> DownloadTextAsync(string url, IReadOnlyDictionary<string, string> headers, string workFile, CancellationToken cancellationToken) =>
            Task.FromResult(Playlists.TryGetValue(url, out string? text)
                ? ToolResult<string>.Success(text)
                : ToolResult<string>.Failure(new ToolError("yt-dlp", "404", "hint")));

        public Task<ToolResult<Done>> DownloadPiecesAsync(
            IReadOnlyList<StreamPiece> pieces, IReadOnlyDictionary<string, string> headers, string outputPath, Action<int> onPieceDone, CancellationToken cancellationToken)
        {
            PieceDownloads.Add((pieces, headers));
            if (PieceFailure is { } failure)
            {
                return Task.FromResult(ToolResult.Failure(failure));
            }

            for (int index = 1; index <= pieces.Count; index++)
            {
                onPieceDone(index);
            }

            File.WriteAllText(outputPath, "pieces");
            return Task.FromResult(ToolResult.Success());
        }
    }
}
