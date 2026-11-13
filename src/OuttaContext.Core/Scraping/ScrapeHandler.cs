using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;

namespace OuttaContext.Core.Scraping;

/// <summary>`outta scrape SITE PAGE_URL`: fetch the page and list the video links on it.</summary>
public sealed class ScrapeHandler(
    ScraperRegistry scrapers, IPageFetcher fetcher, IPrefixStore prefixes, ILibraryRepository repository, IReporter reporter)
{
    public async Task<IReadOnlyList<FoundVideo>> HandleAsync(string site, string pageUrl, CancellationToken cancellationToken)
    {
        Scraper scraper = scrapers.Get(site);
        IReadOnlyList<string> extra = prefixes.ForSite(scraper.Site);

        reporter.Step($"Fetching {pageUrl}");
        string html = (await fetcher.FetchAsync(pageUrl, cancellationToken)).ValueOrThrow();

        reporter.Step($"Looking for {scraper.RuleCount(extra)} kinds of {scraper.Label} links");
        IReadOnlyList<FoundVideo> found = scraper.Extract(html, pageUrl, extra);
        if (found.Count == 0)
        {
            throw new NoVideosFoundException($"No {scraper.Label} videos found on {pageUrl}.");
        }

        // Remember the page, so `outta clip` can send it as the referer for embed-only videos.
        repository.RememberLinks(found.Select(video => (video.Url, video.PageUrl, video.Kind)));
        return found;
    }
}
