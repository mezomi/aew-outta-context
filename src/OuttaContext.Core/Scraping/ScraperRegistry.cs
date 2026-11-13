using OuttaContext.Core.Common;

namespace OuttaContext.Core.Scraping;

/// <summary>All scrapers. `outta scrape SITE` commands are generated from this list.</summary>
public sealed class ScraperRegistry
{
    public ScraperRegistry(IEnumerable<Scraper> scrapers)
    {
        Scrapers = [.. scrapers];
    }

    /// <summary>Dailymotion and YouTube.</summary>
    public static ScraperRegistry Default { get; } = new([new DailymotionScraper(), new YouTubeScraper()]);

    public IReadOnlyList<Scraper> Scrapers { get; }

    public IEnumerable<string> Sites => Scrapers.Select(scraper => scraper.Site);

    public Result<Scraper, Error> Find(string site) =>
        Scrapers.TryFirst(scraper => string.Equals(scraper.Site, site, StringComparison.OrdinalIgnoreCase))
            .ToResult(ScrapeErrors.UnknownSite(site, Sites));
}
