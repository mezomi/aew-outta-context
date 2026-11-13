using OuttaContext.Core.Results;

namespace OuttaContext.Core.Scraping;

/// <summary>Downloads a web page's HTML.</summary>
public interface IPageFetcher
{
    Task<ToolResult<string>> FetchAsync(string url, CancellationToken cancellationToken);
}
