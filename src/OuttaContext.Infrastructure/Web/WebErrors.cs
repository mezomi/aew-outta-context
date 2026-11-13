using OuttaContext.Core.Common;

namespace OuttaContext.Infrastructure.Web;

/// <summary>Errors from fetching web pages.</summary>
public static class WebErrors
{
    public static Error HttpStatus(string url, int status) =>
        new("web.http_status", $"{url} answered with HTTP {status}.", "The page may need a login or block scripts. Copy the video link from your browser.");

    public static Error Unreachable(string url, string problem) =>
        new("web.unreachable", $"Could not fetch {url}: {problem}", "Check the address and your internet connection.");

    public static Error TimedOut(string url) =>
        new("web.timeout", $"{url} took too long to answer.", "Check your connection and try again.");
}
