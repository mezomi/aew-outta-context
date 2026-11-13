namespace OuttaContext.Core.Sources;

/// <summary>
/// One way of asking for a video. Sites sometimes refuse one style of request but accept
/// another, so a source can offer several; they are tried in order until one works.
/// </summary>
/// <param name="Label">Shown to the user, e.g. "embedded player".</param>
/// <param name="Url">The video URL to ask yt-dlp about.</param>
/// <param name="Headers">Extra HTTP headers, e.g. Referer.</param>
/// <param name="ExtractorQuery">Extra query values for yt-dlp's site extractor (Dailymotion only); may be empty.</param>
public sealed record RequestVariant(
    string Label,
    string Url,
    RequestHeaders Headers,
    IReadOnlyDictionary<string, string> ExtractorQuery)
{
    public static readonly IReadOnlyDictionary<string, string> NoQuery = new Dictionary<string, string>();

    public RequestVariant(string label, string url, RequestHeaders headers)
        : this(label, url, headers, NoQuery)
    {
    }
}
