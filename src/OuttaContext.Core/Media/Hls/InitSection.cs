namespace OuttaContext.Core.Media.Hls;

/// <summary>The header piece of a fragmented MP4 stream (<c>#EXT-X-MAP</c>).</summary>
public sealed record InitSection(string Url, Maybe<ByteRange> Range);
