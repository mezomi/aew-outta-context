using System.Net;
using System.Text.RegularExpressions;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Scraping;

/// <summary>
/// Finds one site's video links in a page's HTML: built-in <see cref="LinkRules"/> plus link
/// prefixes the user registered (any link starting with a prefix counts as a video).
/// </summary>
public abstract partial class Scraper
{
    /// <summary>Command name, e.g. "dailymotion" for `outta scrape dailymotion`.</summary>
    public abstract string Site { get; }

    public abstract string Label { get; }

    public abstract IReadOnlyList<LinkRule> LinkRules { get; }

    /// <summary>Turns found IDs into watch URLs.</summary>
    public abstract VideoSource Source { get; }

    public int RuleCount(IReadOnlyCollection<LinkPrefix> prefixes) => LinkRules.Count + prefixes.Count;

    /// <summary>Every video link on the page, in page order, without duplicates.</summary>
    public IReadOnlyList<FoundVideo> Extract(string html, string pageUrl, IReadOnlyCollection<LinkPrefix> prefixes)
    {
        string text = WebUtility.HtmlDecode(html).Replace(@"\/", "/", StringComparison.Ordinal);
        string lowered = text.ToLowerInvariant();
        var hits = new List<(int Position, string VideoId, string Kind)>();

        foreach (LinkRule rule in LinkRules)
        {
            if (rule.RequiresText is { } required && !lowered.Contains(required, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in rule.Regex.Matches(text))
            {
                hits.Add((match.Index, match.Groups[1].Value, rule.Kind));
            }
        }

        if (prefixes.Count > 0)
        {
            var bare = prefixes.Select(prefix => StripScheme(prefix.Value)).ToList();
            foreach (Match match in LinkRegex().Matches(text))
            {
                string link = match.Value;
                if (bare.Any(StripScheme(link).StartsWith) && IdFromLink(link).TryGetValue(out string? videoId))
                {
                    hits.Add((match.Index, videoId, "registered prefix"));
                }
            }
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return hits
            .OrderBy(hit => hit.Position)
            .Select(hit => new FoundVideo(Source.UrlForId(hit.VideoId), hit.VideoId, hit.Kind, pageUrl))
            .Where(found => seen.Add(found.Url))
            .ToList();
    }

    /// <summary>A video ID from a link: its video=/v=/id= value, or else the last part of its path.</summary>
    public static Maybe<string> IdFromLink(string link)
    {
        string absolute = link.Contains("://", StringComparison.Ordinal) ? link : "https:" + link;
        if (!Uri.TryCreate(absolute, UriKind.Absolute, out Uri? uri))
        {
            return Maybe<string>.None;
        }

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => Uri.UnescapeDataString(pair[1]), StringComparer.Ordinal);
        foreach (string key in new[] { "video", "v", "id" })
        {
            if (query.TryGetValue(key, out string? candidate))
            {
                return VideoIdRegex().IsMatch(candidate) ? candidate : Maybe<string>.None;
            }
        }

        string? last = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (last is null)
        {
            return Maybe<string>.None;
        }

        last = PageExtensionRegex().Replace(last, string.Empty);
        return VideoIdRegex().IsMatch(last) ? last : Maybe<string>.None;
    }

    private static string StripScheme(string url) => SchemeRegex().Replace(url.Trim(), string.Empty).ToLowerInvariant();

    [GeneratedRegex(@"(?:https?:)?//[^\s""'<>()\\]+", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^[\w-]{5,}$")]
    private static partial Regex VideoIdRegex();

    [GeneratedRegex(@"\.(html?|js|php)$", RegexOptions.IgnoreCase)]
    private static partial Regex PageExtensionRegex();

    [GeneratedRegex(@"^(?:https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();
}
