using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media.Hls;

/// <summary>One piece of an HLS stream (usually a few seconds long).</summary>
/// <param name="Range">Set when the piece is part of a bigger file.</param>
public sealed record HlsSegment(string Url, VideoTime Start, VideoTime Duration, Maybe<ByteRange> Range)
{
    public VideoTime End => Start.Plus(Duration);
}
