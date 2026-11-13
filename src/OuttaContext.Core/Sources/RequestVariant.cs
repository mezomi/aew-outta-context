namespace OuttaContext.Core.Sources;

/// <summary>
/// One way of asking for a video. Sites sometimes refuse one style of request but accept
/// another, so a source can offer several; they are tried in order until one works.
/// </summary>
/// <param name="Label">Shown to the user, e.g. "embedded player".</param>
/// <param name="Url">The video URL to ask yt-dlp about.</param>
/// <param name="Headers">Extra HTTP headers, e.g. Referer.</param>
/// <param name="ExtractorQuery">Extra query values for yt-dlp's site extractor (Dailymotion only).</param>
public sealed record RequestVariant(
    string Label,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string>? ExtractorQuery = null)
{
    public static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>();

    public static IReadOnlyDictionary<string, string> RefererHeader(string? pageUrl) =>
        pageUrl is null ? NoHeaders : new Dictionary<string, string> { ["Referer"] = pageUrl };
}
