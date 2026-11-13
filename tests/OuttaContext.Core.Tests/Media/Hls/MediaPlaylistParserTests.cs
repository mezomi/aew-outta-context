using OuttaContext.Core.Media.Hls;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Core.Tests.Media.Hls;

public sealed class MediaPlaylistParserTests
{
    private static readonly Uri BaseUrl = new("https://cdn.example/video/h264/manifest.m3u8");

    [Fact]
    public void Parse_FragmentedMp4Playlist_ReadsPiecesWithRunningStartTimes()
    {
        MediaPlaylist playlist = Parse("fmp4_media.m3u8");

        playlist.Segments.Select(s => s.Start).ShouldBe([0, 6, 12, 18, 24]);
        playlist.Duration.ShouldBe(28.5);
    }

    [Fact]
    public void Parse_RelativeAndAbsoluteUrls_AreResolved()
    {
        MediaPlaylist playlist = Parse("fmp4_media.m3u8");

        playlist.InitUrl.ShouldBe("https://cdn.example/video/h264/init.mp4");
        playlist.Segments[0].Url.ShouldBe("https://cdn.example/video/h264/seg-0.m4s");
        playlist.Segments[^1].Url.ShouldBe("https://cdn.example/other/seg-4.m4s");
    }

    [Fact]
    public void Parse_ByteRanges_IncludingImplicitOffsets()
    {
        MediaPlaylist playlist = Parse("ts_byterange.m3u8");

        playlist.InitRange.ShouldBe(new ByteRange(0, 99));
        playlist.Segments.Select(s => s.Range).ShouldBe([new ByteRange(100, 1099), new ByteRange(1100, 2099)]);
    }

    [Fact]
    public void Parse_EncryptedPlaylist_IsFlagged()
    {
        Parse("encrypted.m3u8").Encrypted.ShouldBeTrue();
    }

    [Theory]
    [InlineData("<html>", "did not return an HLS playlist")]
    [InlineData("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\n720.m3u8", "master playlist")]
    [InlineData("#EXTM3U\nseg.ts\n", "without a duration")]
    public void Parse_UnusablePlaylist_Fails(string text, string message)
    {
        var result = MediaPlaylistParser.Parse(text, BaseUrl);

        result.Failed.ShouldBeTrue();
        result.Error.Message.ShouldContain(message);
    }

    [Theory]
    [InlineData(7, 11, new double[] { 6 })]
    [InlineData(5, 13, new double[] { 0, 6, 12 })]
    [InlineData(24, 30, new double[] { 24 })]
    [InlineData(40, 50, new double[] { })]
    public void Covering_Range_ReturnsOnlyOverlappingPieces(double start, double end, double[] expectedStarts)
    {
        Parse("fmp4_media.m3u8").Covering(start, end).Select(s => s.Start).ShouldBe(expectedStarts);
    }

    private static MediaPlaylist Parse(string fixture) =>
        MediaPlaylistParser.Parse(Fixtures.Read("hls", fixture), BaseUrl).ValueOrThrow();
}
