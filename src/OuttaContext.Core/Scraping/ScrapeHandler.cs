using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Reporting;

namespace OuttaContext.Core.Scraping;

/// <summary>`outta scrape SITE PAGE_URL`: fetch the page and list the video links on it.</summary>
public sealed class ScrapeHandler(
    ScraperRegistry scrapers, IPageFetcher fetcher, IPrefixStore prefixes, ILibraryRepository repository, IReporter reporter)
{
    public Task<Result<IReadOnlyList<FoundVideo>, Error>> HandleAsync(string site, string pageUrl, CancellationToken cancellationToken) =>
        scrapers.Find(site)
            .Bind(scraper => WebUrl.Create(pageUrl).Map(_ => scraper))
            .Bind(scraper => ScrapeAsync(scraper, pageUrl, cancellationToken))
            .Tap(Remember);

    private async Task<Result<IReadOnlyList<FoundVideo>, Error>> ScrapeAsync(Scraper scraper, string pageUrl, CancellationToken cancellationToken)
    {
        reporter.Step($"Fetching {pageUrl}");
        Result<string, Error> page = await fetcher.FetchAsync(pageUrl, cancellationToken);
        return page
            .Map(html => FindVideos(scraper, html, pageUrl))
            .Ensure(videos => videos.Count > 0, ScrapeErrors.NoVideosFound(scraper.Label, pageUrl));
    }

    private IReadOnlyList<FoundVideo> FindVideos(Scraper scraper, string html, string pageUrl)
    {
        IReadOnlyList<LinkPrefix> extra = prefixes.ForSite(scraper.Site);
        reporter.Step($"Looking for {scraper.RuleCount(extra)} kinds of {scraper.Label} links");
        return scraper.Extract(html, pageUrl, extra);
    }

    /// <summary>Remembers the page, so `outta clip` can send it as the referer for embed-only videos.</summary>
    private void Remember(IReadOnlyList<FoundVideo> videos) =>
        repository.RememberLinks(videos.Select(video => new ScrapedLink(video.Url, video.PageUrl, video.Kind)));
}
