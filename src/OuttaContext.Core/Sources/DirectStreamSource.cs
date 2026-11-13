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

    public override string? VideoId(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !StreamPathRegex().IsMatch(uri.AbsolutePath))
        {
            return null;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim()));
        return Convert.ToHexStringLower(hash)[..12];
    }

    public override string UrlForId(string videoId) =>
        throw new NotSupportedException("Direct links have no standard form.");

    public override string CanonicalUrl(string url) =>
        VideoId(url) is not null ? url.Trim() : throw new ArgumentException($"{url} is not a direct stream link.", nameof(url));

    public override IReadOnlyList<RequestVariant> RequestVariants(string url, string? pageUrl)
    {
        string canonical = CanonicalUrl(url);
        string host = new Uri(canonical).Host;
        var variants = new List<RequestVariant>();
        if (host.Contains("dmcdn", StringComparison.OrdinalIgnoreCase)
            || host.Contains("dailymotion", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add(new RequestVariant("as the Dailymotion player", canonical, DailymotionSource.PlayerHeaders()));
        }

        variants.Add(new RequestVariant("direct", canonical, RequestVariant.RefererHeader(pageUrl)));
        return variants;
    }

    [GeneratedRegex(@"\.(m3u8|mp4|mpd|m4v|webm)$", RegexOptions.IgnoreCase)]
    private static partial Regex StreamPathRegex();
}
