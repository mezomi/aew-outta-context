namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>Settings every yt-dlp call shares. A command can change them before it runs (e.g. --cookies-from-browser).</summary>
public sealed class YtDlpOptions
{
    /// <summary>e.g. "firefox" or "chrome:Profile 1"; null for none.</summary>
    public string? CookiesFromBrowser { get; set; }

    /// <summary>A cookies.txt (Netscape format); null for none.</summary>
    public string? CookieFile { get; set; }

    /// <summary>Whether to ask yt-dlp for its [debug] lines (true with --verbose). Checked on every call.</summary>
    public Func<bool> Debug { get; set; } = () => false;

    /// <summary>The browsers to pose as, in order; null means a plain request.</summary>
    public IReadOnlyList<string?> ImpersonateTargets { get; init; } = ["chrome", "firefox", null];

    /// <summary>Arguments added to every yt-dlp command.</summary>
    public IEnumerable<string> CommonArguments()
    {
        yield return "--ignore-config";
        yield return "--no-color";
        if (CookiesFromBrowser is { } browser)
        {
            yield return "--cookies-from-browser";
            yield return browser;
        }

        if (CookieFile is { } file)
        {
            yield return "--cookies";
            yield return file;
        }

        if (Debug())
        {
            yield return "--verbose";
        }
    }
}
