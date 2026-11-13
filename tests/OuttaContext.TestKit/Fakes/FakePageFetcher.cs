using OuttaContext.Core.Common;
using OuttaContext.Core.Scraping;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Serves fixed HTML by URL; unknown URLs fail like a 404.</summary>
public sealed class FakePageFetcher : IPageFetcher
{
    public Dictionary<string, string> Pages { get; } = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public Task<Result<string, Error>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        Requests.Add(url);
        return Task.FromResult(Pages.TryGetValue(url, out string? html)
            ? Result.Success<string, Error>(html)
            : Result.Failure<string, Error>(new Error("web.http_status", $"{url} answered with HTTP 404.", "Check the address.")));
    }
}
