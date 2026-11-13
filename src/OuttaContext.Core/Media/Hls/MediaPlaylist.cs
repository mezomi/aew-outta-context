namespace OuttaContext.Core.Media.Hls;

/// <summary>A byte range inside a file, both ends included.</summary>
public readonly record struct ByteRange(long First, long Last);

/// <summary>One piece of an HLS stream (usually a few seconds long).</summary>
public sealed record HlsSegment(string Url, double Start, double Duration, ByteRange? Range = null)
{
    public double End => Start + Duration;
}

/// <summary>
/// An HLS media playlist (.m3u8): the list of pieces a stream is made of. For fragmented MP4
/// streams, <see cref="InitUrl"/> is the header piece every other piece needs.
/// </summary>
public sealed record MediaPlaylist(
    IReadOnlyList<HlsSegment> Segments,
    string? InitUrl = null,
    ByteRange? InitRange = null,
    bool Encrypted = false)
{
    public double Duration => Segments.Count == 0 ? 0 : Segments[^1].End;

    /// <summary>The pieces that overlap <c>[start, end)</c>.</summary>
    public IReadOnlyList<HlsSegment> Covering(double start, double end) =>
        Segments.Where(segment => segment.End > start && segment.Start < end).ToList();
}
