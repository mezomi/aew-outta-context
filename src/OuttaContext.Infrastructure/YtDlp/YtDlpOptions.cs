using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Settings every yt-dlp call shares. One instance for the whole run: it starts from settings.toml
/// and a command can change it before it runs (e.g. --cookies-from-browser, --verbose).
/// </summary>
public sealed class YtDlpOptions(IReporter reporter)
{
    /// <summary>e.g. "firefox" or "chrome:Profile 1".</summary>
    public Maybe<BrowserName> CookiesFromBrowser { get; set; }

    /// <summary>A cookies.txt (Netscape format).</summary>
    public Maybe<string> CookieFile { get; set; }

    /// <summary>Whether to ask yt-dlp for its [debug] lines (true with --verbose).</summary>
    public bool Debug => reporter.IsVerbose;

    /// <summary>The browsers to pose as, in order; none means a plain request.</summary>
    public IReadOnlyList<Maybe<string>> ImpersonateTargets { get; init; } = ["chrome", "firefox", Maybe<string>.None];

    /// <summary>Arguments added to every yt-dlp command.</summary>
    public IEnumerable<string> CommonArguments()
    {
        yield return "--ignore-config";
        yield return "--no-color";
        if (CookiesFromBrowser.TryGetValue(out BrowserName? browser))
        {
            yield return "--cookies-from-browser";
            yield return browser.Value;
        }

        if (CookieFile.TryGetValue(out string? file))
        {
            yield return "--cookies";
            yield return file;
        }

        if (Debug)
        {
            yield return "--verbose";
        }
    }
}
