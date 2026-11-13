using System.Text.RegularExpressions;

namespace OuttaContext.Core.Sources;

public sealed partial class DailymotionSource : VideoSource
{
    public const string PlayerOrigin = "https://geo.dailymotion.com";

    /// <summary>The headers Dailymotion's own player sends.</summary>
    public static readonly RequestHeaders PlayerHeaders =
        RequestHeaders.From([new("Referer", PlayerOrigin + "/"), new("Origin", PlayerOrigin)]);

    public override string Name => "dailymotion";

    public override string Label => "Dailymotion";

    protected override IReadOnlyList<Regex> UrlRegexes =>
        [VideoUrlRegex(), GeoPlayerUrlRegex(), PlayerHtmlUrlRegex(), ShortUrlRegex()];

    public override string UrlForId(string videoId) => $"https://www.dailymotion.com/video/{videoId}";

    /// <summary>
    /// Embed-only videos (IDs often start with "k") answer HTTP 403 unless the request looks like
    /// it comes from Dailymotion's player on an allowed site. So when the page is known, ask first
    /// as that player, then with the page as referer, then plainly.
    /// </summary>
    public override IReadOnlyList<RequestVariant> RequestVariants(VideoLink link, Maybe<string> pageUrl)
    {
        var variants = new List<RequestVariant>();
        if (pageUrl.TryGetValue(out string? page))
        {
            var query = new Dictionary<string, string>
            {
                ["app"] = "com.dailymotion.neon",
                ["embedder"] = page,
                ["referer"] = page,
                ["client_type"] = "webapp",
                ["section_type"] = "player",
                ["component_style"] = "_",
                ["geo"] = "1",
            };
            Match player = PlayerIdRegex().Match(link.Original);
            if (player.Success)
            {
                query["player-id"] = player.Groups[1].Value;
            }

            variants.Add(new RequestVariant("embedded player", link.CanonicalUrl, PlayerHeaders, query));
            variants.Add(new RequestVariant("direct, page as referer", link.CanonicalUrl, RequestHeaders.RefererFor(page)));
        }

        variants.Add(new RequestVariant("direct", link.CanonicalUrl, RequestHeaders.None));
        return variants;
    }

    [GeneratedRegex(@"^(?:https?:)?//(?:www\.|m\.)?dailymotion\.com/(?:embed/)?video/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex VideoUrlRegex();

    [GeneratedRegex(
        @"^(?:https?:)?//geo\.dailymotion\.com/player(?:/[\w-]+)?\.html\?(?:.*&)?video=([a-zA-Z0-9]{5,})",
        RegexOptions.IgnoreCase)]
    private static partial Regex GeoPlayerUrlRegex();

    [GeneratedRegex(
        @"^(?:https?:)?//(?:www\.|m\.)?dailymotion\.com/player\.html\?(?:.*&)?video=([a-zA-Z0-9]{5,})",
        RegexOptions.IgnoreCase)]
    private static partial Regex PlayerHtmlUrlRegex();

    [GeneratedRegex(@"^(?:https?:)?//(?:www\.)?dai\.ly/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortUrlRegex();

    [GeneratedRegex(@"geo\.dailymotion\.com/player/([\w-]+)\.html", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerIdRegex();
}
