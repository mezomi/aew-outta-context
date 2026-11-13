namespace OuttaContext.Core.Library;

/// <summary>A video link found by `outta scrape`, and the page it was on (used as referer later).</summary>
/// <param name="Kind">How it was found, e.g. "embed" or "link".</param>
public sealed record ScrapedLink(string VideoUrl, string PageUrl, string Kind);
