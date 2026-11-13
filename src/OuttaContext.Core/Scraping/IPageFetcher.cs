using OuttaContext.Core.Common;

namespace OuttaContext.Core.Scraping;

/// <summary>Downloads a web page's HTML.</summary>
public interface IPageFetcher
{
    Task<Result<string, Error>> FetchAsync(string url, CancellationToken cancellationToken);
}
