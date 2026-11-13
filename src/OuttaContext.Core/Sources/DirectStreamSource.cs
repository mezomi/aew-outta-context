using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OuttaContext.Core.Sources;

/// <summary>
/// A direct stream link (.m3u8, .mp4, .mpd), e.g. copied from the browser's network tab.
/// The fallback when a site blocks yt-dlp's own extractor.
/// </summary>
public sealed partial class DirectStreamSource : VideoSource
{
    public override string Name => "direct";

    public override string Label => "Direct stream link";

    protected override IReadOnlyList<Regex> UrlRegexes => [];

    /// <summary>Any http(s) link whose path ends in a stream extension. Its ID is a hash of the link.</summary>
    public override Maybe<VideoLink> Recognize(string url)
    {
        string trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !StreamPathRegex().IsMatch(uri.AbsolutePath))
        {
            return Maybe<VideoLink>.None;
        }

        string id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed)))[..12];
        return new VideoLink(this, id, trimmed, trimmed);
    }

    /// <summary>Direct links have no standard form; the link itself is used.</summary>
    public override string UrlForId(string videoId) => videoId;

    public override IReadOnlyList<RequestVariant> RequestVariants(VideoLink link, Maybe<string> pageUrl)
    {
        string host = new Uri(link.CanonicalUrl).Host;
        var variants = new List<RequestVariant>();
        if (host.Contains("dmcdn", StringComparison.OrdinalIgnoreCase)
            || host.Contains("dailymotion", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add(new RequestVariant("as the Dailymotion player", link.CanonicalUrl, DailymotionSource.PlayerHeaders));
        }

        variants.Add(new RequestVariant("direct", link.CanonicalUrl, RequestHeaders.RefererFor(pageUrl)));
        return variants;
    }

    [GeneratedRegex(@"\.(m3u8|mp4|mpd|m4v|webm)$", RegexOptions.IgnoreCase)]
    private static partial Regex StreamPathRegex();
}
