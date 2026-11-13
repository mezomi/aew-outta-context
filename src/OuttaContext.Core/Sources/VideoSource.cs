using System.Text.RegularExpressions;

namespace OuttaContext.Core.Sources;

/// <summary>
/// A site we can clip from. A subclass lists URL regexes whose first group is the video ID.
/// To support a new site: add a subclass and add it to <see cref="SourceRegistry"/>.
/// </summary>
public abstract class VideoSource
{
    /// <summary>Short key stored with clips, e.g. "youtube".</summary>
    public abstract string Name { get; }

    /// <summary>Shown to the user, e.g. "YouTube".</summary>
    public abstract string Label { get; }

    protected abstract IReadOnlyList<Regex> UrlRegexes { get; }

    /// <summary>The link as one of this site's videos, or none if it isn't one.</summary>
    public virtual Maybe<VideoLink> Recognize(string url)
    {
        string trimmed = url.Trim();
        foreach (Regex urlRegex in UrlRegexes)
        {
            Match match = urlRegex.Match(trimmed);
            if (match.Success)
            {
                string id = match.Groups[1].Value;
                return new VideoLink(this, id, UrlForId(id), trimmed);
            }
        }

        return Maybe<VideoLink>.None;
    }

    /// <summary>The standard watch URL for a video ID.</summary>
    public abstract string UrlForId(string videoId);

    /// <summary>The ways to ask for this video, best first.</summary>
    public virtual IReadOnlyList<RequestVariant> RequestVariants(VideoLink link, Maybe<string> pageUrl) =>
        [new RequestVariant("direct", link.CanonicalUrl, RequestHeaders.RefererFor(pageUrl))];

    public override string ToString() => Label;
}
