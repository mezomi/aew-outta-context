using OuttaContext.Core.Common;

namespace OuttaContext.Core.Sources;

/// <summary>Errors for video links.</summary>
public static class SourceErrors
{
    public static Error Unsupported(string url) =>
        new(
            "source.unsupported",
            $"Not a Dailymotion or YouTube video link (or a direct .m3u8/.mp4 link): {url}",
            "Use a Dailymotion or YouTube video link, or a direct .m3u8/.mp4 link. `outta scrape` can find links on a page.");

    public static Error NoRequestVariants(VideoLink link) =>
        new("source.no_variants", $"There is no way to request {link.CanonicalUrl}.", string.Empty);
}
