using System.Net;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.Infrastructure.Web;

namespace OuttaContext.Infrastructure.Tests.Web;

public sealed class HttpPageFetcherTests : IDisposable
{
    private readonly FakeHttpHandler _http = new();

    public void Dispose() => _http.Dispose();

    [Fact]
    public async Task FetchAsync_Page_ReturnsItsHtml()
    {
        _http.Text("https://example.com/", "<html>hi</html>");

        (await new HttpPageFetcher(_http.Client()).FetchAsync("https://example.com/", CancellationToken.None)).Value.ShouldBe("<html>hi</html>");
    }

    [Fact]
    public async Task FetchAsync_Forbidden_SaysToCopyTheLinkFromTheBrowser()
    {
        _http.Text("https://example.com/", string.Empty, HttpStatusCode.Forbidden);

        var result = await new HttpPageFetcher(_http.Client()).FetchAsync("https://example.com/", CancellationToken.None);

        result.Error.Message.ShouldContain("HTTP 403");
        result.Error.Hint.ShouldContain("browser");
    }

    [Fact]
    public async Task FetchAsync_NotAWebAddress_FailsWithoutARequest()
    {
        var result = await new HttpPageFetcher(_http.Client()).FetchAsync("example.com", CancellationToken.None);

        result.Error.Message.ShouldContain("not a web address");
        _http.Requests.ShouldBeEmpty();
    }
}
