using OuttaContext.Core.Media.Hls;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Core.Tests.Media.Hls;

public sealed class MediaPlaylistParserTests
{
    private static readonly Uri BaseUrl = new("https://cdn.example/video/h264/manifest.m3u8");

    [Fact]
    public void Parse_FragmentedMp4Playlist_ReadsPiecesWithRunningStartTimes()
    {
        MediaPlaylist playlist = Parse("fmp4_media.m3u8");

        playlist.Segments.Select(s => s.Start.Seconds).ShouldBe([0, 6, 12, 18, 24]);
        playlist.Duration.Seconds.ShouldBe(28.5);
    }

    [Fact]
    public void Parse_RelativeAndAbsoluteUrls_AreResolved()
    {
        MediaPlaylist playlist = Parse("fmp4_media.m3u8");

        playlist.Init.ShouldHaveValue().Url.ShouldBe("https://cdn.example/video/h264/init.mp4");
        playlist.Segments[0].Url.ShouldBe("https://cdn.example/video/h264/seg-0.m4s");
        playlist.Segments[^1].Url.ShouldBe("https://cdn.example/other/seg-4.m4s");
    }

    [Fact]
    public void Parse_ByteRanges_IncludingImplicitOffsets()
    {
        MediaPlaylist playlist = Parse("ts_byterange.m3u8");

        playlist.Init.ShouldHaveValue().Range.ShouldHaveValue().ToString().ShouldBe("0-99");
        playlist.Segments.Select(s => s.Range.ShouldHaveValue().ToString()).ShouldBe(["100-1099", "1100-2099"]);
    }

    [Fact]
    public void Parse_EncryptedPlaylist_IsFlagged()
    {
        Parse("encrypted.m3u8").Encrypted.ShouldBeTrue();
    }

    [Theory]
    [InlineData("<html>", "hls.not_a_playlist")]
    [InlineData("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1\n720.m3u8", "hls.master_playlist")]
    [InlineData("#EXTM3U\nseg.ts\n", "hls.no_duration")]
    [InlineData("#EXTM3U\n#EXTINF:-2,\nseg.ts\n", "hls.no_duration")]
    [InlineData("#EXTM3U\n#EXT-X-BYTERANGE:lots\n#EXTINF:2,\nseg.ts\n", "hls.byte_range")]
    [InlineData("#EXTM3U\n#EXT-X-BYTERANGE:0@10\n#EXTINF:2,\nseg.ts\n", "hls.byte_range")]
    public void Parse_UnusablePlaylist_FailsWithReason(string text, string code)
    {
        MediaPlaylistParser.Parse(text, BaseUrl).ShouldFailWith(code);
    }

    [Theory]
    [InlineData(7, 11, new double[] { 6 })]
    [InlineData(5, 13, new double[] { 0, 6, 12 })]
    [InlineData(24, 30, new double[] { 24 })]
    [InlineData(40, 50, new double[] { })]
    public void Covering_Range_ReturnsOnlyOverlappingPieces(double start, double end, double[] expectedStarts)
    {
        Parse("fmp4_media.m3u8").Covering(Values.Time(start), Values.Time(end)).Select(s => s.Start.Seconds).ShouldBe(expectedStarts);
    }

    private static MediaPlaylist Parse(string fixture) =>
        MediaPlaylistParser.Parse(Fixtures.Read("hls", fixture), BaseUrl).ShouldSucceed();
}
