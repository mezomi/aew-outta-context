namespace OuttaContext.Core.Sources;

/// <summary>
/// A link we know how to clip from: which site it is, the video's ID there, and the standard
/// address for it. Made by <see cref="SourceRegistry.Detect"/>, so it is always recognised.
/// </summary>
/// <param name="Original">The link as the user gave it (it can carry extra details, e.g. a Dailymotion player ID).</param>
public sealed record VideoLink(VideoSource Source, string VideoId, string CanonicalUrl, string Original)
{
    /// <summary>The ways to ask for this video, best first.</summary>
    public IReadOnlyList<RequestVariant> RequestVariants(Maybe<string> pageUrl) => Source.RequestVariants(this, pageUrl);
}
