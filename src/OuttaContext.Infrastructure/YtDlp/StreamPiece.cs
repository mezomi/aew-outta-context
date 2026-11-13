using OuttaContext.Core.Media.Hls;
using OuttaContext.Core.Time;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>One piece of a stream to download (an HLS segment or its header piece).</summary>
/// <param name="Range">Set when the piece is only part of the file at <paramref name="Url"/>.</param>
public sealed record StreamPiece(string Url, Maybe<VideoTime> Duration = default, Maybe<ByteRange> Range = default);
