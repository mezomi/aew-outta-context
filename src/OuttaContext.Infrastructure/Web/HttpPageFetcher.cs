using System.Net;
using OuttaContext.Core.Results;
using OuttaContext.Core.Scraping;

namespace OuttaContext.Infrastructure.Web;

/// <summary>Downloads a web page's HTML with a normal browser User-Agent.</summary>
public sealed class HttpPageFetcher(HttpClient client) : IPageFetcher
{
    public const string Tool = "web";
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0 Safari/537.36";

    public static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    public async Task<ToolResult<string>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new ToolError(Tool, $"{url} is not a web address.", "Page URLs start with https://");
        }

        try
        {
            using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ToolError(
                    Tool,
                    $"{url} answered with HTTP {(int)response.StatusCode}.",
                    "The page may need a login or block scripts. Copy the video link from your browser.");
            }

            return ToolResult<string>.Success(await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return new ToolError(Tool, $"Could not fetch {url}: {exception.Message}", "Check the address and your internet connection.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ToolError(Tool, $"{url} took too long to answer.", "Check your connection and try again.");
        }
    }
}
