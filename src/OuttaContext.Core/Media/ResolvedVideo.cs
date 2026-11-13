namespace OuttaContext.Core.Media;

/// <summary>A video plus the streams chosen for it (best quality up to the user's maximum).</summary>
public sealed record ResolvedVideo(VideoInfo Info, IReadOnlyList<StreamFormat> Streams)
{
    public bool IsHls => Streams.Count > 0 && Streams.All(stream => stream.IsHls);
}
