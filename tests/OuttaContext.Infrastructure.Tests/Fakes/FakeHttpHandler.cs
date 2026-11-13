using System.Net;

namespace OuttaContext.Infrastructure.Tests.Fakes;

/// <summary>An HttpClient handler that answers from a dictionary of URL → response.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    public Dictionary<string, Func<HttpResponseMessage>> Responses { get; } = new(StringComparer.Ordinal);

    public List<Uri> Requests { get; } = [];

    public HttpClient Client() => new(this);

    public void Text(string url, string body, HttpStatusCode status = HttpStatusCode.OK) =>
        Responses[url] = () => new HttpResponseMessage(status) { Content = new StringContent(body) };

    public void Bytes(string url, byte[] body) =>
        Responses[url] = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(Responses.TryGetValue(request.RequestUri!.ToString(), out var respond)
            ? respond()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
