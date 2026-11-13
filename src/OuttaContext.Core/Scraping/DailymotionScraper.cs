using System.Text.RegularExpressions;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Scraping;

public sealed partial class DailymotionScraper : Scraper
{
    public override string Site => "dailymotion";

    public override string Label => "Dailymotion";

    public override VideoSource Source { get; } = new DailymotionSource();

    public override IReadOnlyList<LinkRule> LinkRules { get; } =
    [
        new(GeoPlayerRegex(), "geo player embed"),
        new(EmbedIframeRegex(), "embed iframe"),
        new(PlayerHtmlRegex(), "player embed"),
        new(VideoLinkRegex(), "video link"),
        new(ShortLinkRegex(), "short link"),
        new(DataVideoAttributeRegex(), "geo player script", RequiresText: "dailymotion"),
    ];

    [GeneratedRegex(@"geo\.dailymotion\.com/player(?:/[\w-]+)?\.html\?[^\s""'<>]*?\bvideo=([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex GeoPlayerRegex();

    [GeneratedRegex(@"dailymotion\.com/embed/video/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex EmbedIframeRegex();

    [GeneratedRegex(@"dailymotion\.com/player\.html\?[^\s""'<>]*?\bvideo=([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerHtmlRegex();

    [GeneratedRegex(@"dailymotion\.com/video/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex VideoLinkRegex();

    [GeneratedRegex(@"\bdai\.ly/([a-zA-Z0-9]{5,})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortLinkRegex();

    [GeneratedRegex(@"data-video(?:-id)?\s*=\s*[""']([a-zA-Z0-9]{5,})[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DataVideoAttributeRegex();
}
