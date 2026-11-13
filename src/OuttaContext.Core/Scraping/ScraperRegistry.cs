using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Scraping;

/// <summary>All scrapers. `outta scrape SITE` commands are generated from this list.</summary>
public sealed class ScraperRegistry(IEnumerable<Scraper> scrapers)
{
    public ScraperRegistry()
        : this([new DailymotionScraper(), new YouTubeScraper()])
    {
    }

    public IReadOnlyList<Scraper> Scrapers { get; } = [.. scrapers];

    public IEnumerable<string> Sites => Scrapers.Select(scraper => scraper.Site);

    public Scraper Get(string site) =>
        Scrapers.FirstOrDefault(scraper => string.Equals(scraper.Site, site, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotFoundException($"Unknown site \"{site}\".", "Sites you can use: " + string.Join(", ", Sites));
}
