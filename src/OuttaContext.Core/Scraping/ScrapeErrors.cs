using OuttaContext.Core.Common;

namespace OuttaContext.Core.Scraping;

/// <summary>Errors for `outta scrape`.</summary>
public static class ScrapeErrors
{
    private const string ListHint = "See registered prefixes with `outta scrape prefixes list`.";

    public static Error UnknownSite(string site, IEnumerable<string> sites) =>
        new("scrape.unknown_site", $"Unknown site \"{site}\".", "Sites you can use: " + string.Join(", ", sites));

    public static Error NoVideosFound(string label, string pageUrl) =>
        new(
            "scrape.none_found",
            $"No {label} videos found on {pageUrl}.",
            "The page may load its player with JavaScript. Play the video in your browser and copy its link instead. " +
            "If the site uses a new embed style, register it with `outta scrape prefixes add SITE PREFIX`.");

    public static Error NotAPrefix(string prefix) =>
        new("prefix.invalid", $"\"{prefix}\" is not a link prefix.", "Prefixes are the start of a link, e.g. \"https://geo.dailymotion.com/player/\".");

    public static Error PrefixExists(string prefix, string site) =>
        new("prefix.exists", $"\"{prefix}\" is already registered for {site}.", ListHint);

    public static Error PrefixNotFound(string prefix, string site) =>
        new("prefix.not_found", $"\"{prefix}\" is not registered for {site}.", ListHint);
}
