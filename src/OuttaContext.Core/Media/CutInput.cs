using OuttaContext.Core.Sources;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>One input for an ffmpeg cut: a local file or a URL, and where in it the clip starts.</summary>
/// <param name="Source">A local file path, or an http(s) address.</param>
/// <param name="Offset">How far into <paramref name="Source"/> the clip starts.</param>
/// <param name="Headers">Sent when <paramref name="Source"/> is an address.</param>
public sealed record CutInput(string Source, VideoTime Offset, RequestHeaders Headers)
{
    public CutInput(string source, VideoTime offset)
        : this(source, offset, RequestHeaders.None)
    {
    }

    public bool IsRemote =>
        Source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
