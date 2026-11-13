namespace OuttaContext.Core.Scraping;

/// <summary>A video link found on a page.</summary>
public sealed record FoundVideo(string Url, string VideoId, string Kind, string PageUrl);
