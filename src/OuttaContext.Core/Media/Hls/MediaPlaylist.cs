using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media.Hls;

/// <summary>
/// An HLS media playlist (.m3u8): the list of pieces a stream is made of. For fragmented MP4
/// streams, <see cref="Init"/> is the header piece every other piece needs.
/// </summary>
public sealed record MediaPlaylist(IReadOnlyList<HlsSegment> Segments, Maybe<InitSection> Init, bool Encrypted)
{
    public VideoTime Duration => Segments.Count == 0 ? VideoTime.Zero : Segments[^1].End;

    /// <summary>The pieces that overlap <c>[start, end)</c>.</summary>
    public IReadOnlyList<HlsSegment> Covering(VideoTime start, VideoTime end) =>
        Segments.Where(segment => segment.End.IsLongerThan(start) && end.IsLongerThan(segment.Start)).ToList();
}
