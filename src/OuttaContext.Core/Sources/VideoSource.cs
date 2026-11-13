using System.Text.RegularExpressions;

namespace OuttaContext.Core.Sources;

/// <summary>
/// A site we can clip from. A subclass lists URL regexes whose first group is the video ID.
/// To support a new site: add a subclass and register it in <see cref="SourceRegistry"/>.
/// </summary>
public abstract class VideoSource
{
    /// <summary>Short key stored with clips, e.g. "youtube".</summary>
    public abstract string Name { get; }

    /// <summary>Shown to the user, e.g. "YouTube".</summary>
    public abstract string Label { get; }

    protected abstract IReadOnlyList<Regex> UrlRegexes { get; }

    public virtual string? VideoId(string url)
    {
        foreach (Regex urlRegex in UrlRegexes)
        {
            Match match = urlRegex.Match(url.Trim());
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    public bool Matches(string url) => VideoId(url) is not null;

    /// <summary>The standard watch URL for a video ID.</summary>
    public abstract string UrlForId(string videoId);

    public virtual string CanonicalUrl(string url) =>
        VideoId(url) is { } id ? UrlForId(id) : throw new ArgumentException($"{url} is not a {Label} link.", nameof(url));

    /// <summary>The ways to ask for this video, best first.</summary>
    public virtual IReadOnlyList<RequestVariant> RequestVariants(string url, string? pageUrl) =>
        [new RequestVariant("direct", CanonicalUrl(url), RequestVariant.RefererHeader(pageUrl))];
}
