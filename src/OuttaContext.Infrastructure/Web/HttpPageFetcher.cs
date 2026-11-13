using System.Net;
using OuttaContext.Core.Common;
using OuttaContext.Core.Scraping;

namespace OuttaContext.Infrastructure.Web;

/// <summary>Downloads a web page's HTML with a normal browser User-Agent.</summary>
/// <remarks>A typed HttpClient: <see cref="Configure"/> sets it up when it is registered (AddHttpClient).</remarks>
public sealed class HttpPageFetcher(HttpClient client) : IPageFetcher
{
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0 Safari/537.36";

    public static void Configure(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    }

    public static HttpMessageHandler CreateHandler() =>
        new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };

    public async Task<Result<string, Error>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        Result<WebUrl, Error> address = WebUrl.Create(url);
        if (address.IsFailure)
        {
            return address.Error;
        }

        try
        {
            using HttpResponseMessage response = await client.GetAsync(address.Value.Uri, cancellationToken);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(cancellationToken)
                : WebErrors.HttpStatus(url, (int)response.StatusCode);
        }
        catch (HttpRequestException exception)
        {
            return WebErrors.Unreachable(url, exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WebErrors.TimedOut(url);
        }
    }
}
