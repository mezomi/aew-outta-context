using System.Text.RegularExpressions;

namespace OuttaContext.Core.Scraping;

/// <summary>A built-in way to spot a video link in HTML. The regex's first group is the video ID.</summary>
/// <param name="Regex">The pattern to search the page for.</param>
/// <param name="Kind">Shown next to results, e.g. "geo player embed".</param>
/// <param name="RequiresText">Only used when this text appears on the page.</param>
public sealed record LinkRule(Regex Regex, string Kind, string? RequiresText = null);
