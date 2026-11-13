using System.Text.RegularExpressions;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Scraping;

public sealed partial class YouTubeScraper : Scraper
{
    public override string Site => "youtube";

    public override string Label => "YouTube";

    public override VideoSource Source { get; } = new YouTubeSource();

    public override IReadOnlyList<LinkRule> LinkRules { get; } =
    [
        new(EmbedIframeRegex(), "embed iframe"),
        new(WatchLinkRegex(), "video link"),
        new(ShortsLinkRegex(), "short"),
        new(LiveLinkRegex(), "live link"),
        new(ShortLinkRegex(), "short link"),
    ];

    [GeneratedRegex(@"youtube(?:-nocookie)?\.com/embed/(?!videoseries)([\w-]{11})(?![\w-])", RegexOptions.IgnoreCase)]
    private static partial Regex EmbedIframeRegex();

    [GeneratedRegex(@"youtube\.com/watch\?(?:[^\s""'<>]*?&)?v=([\w-]{11})(?![\w-])", RegexOptions.IgnoreCase)]
    private static partial Regex WatchLinkRegex();

    [GeneratedRegex(@"youtube\.com/shorts/([\w-]{11})(?![\w-])", RegexOptions.IgnoreCase)]
    private static partial Regex ShortsLinkRegex();

    [GeneratedRegex(@"youtube\.com/live/([\w-]{11})(?![\w-])", RegexOptions.IgnoreCase)]
    private static partial Regex LiveLinkRegex();

    [GeneratedRegex(@"\byoutu\.be/([\w-]{11})(?![\w-])", RegexOptions.IgnoreCase)]
    private static partial Regex ShortLinkRegex();
}
