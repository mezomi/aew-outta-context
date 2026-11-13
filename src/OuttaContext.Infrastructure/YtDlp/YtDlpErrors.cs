using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Common;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Every <see cref="Error"/> that comes from yt-dlp. <see cref="Translate"/> turns yt-dlp's error
/// text into a plain message and a hint; its rules are checked in order and the first match wins.
/// To handle a new yt-dlp message, add a row to <see cref="Rules"/>.
/// </summary>
public static partial class YtDlpErrors
{
    private const string NetworkHint = "Check your connection and try again.";
    private const string UpdateHint = "Run `outta update` (it updates yt-dlp). If it persists, try another upload.";

    private static readonly IReadOnlyList<ErrorRule> Rules =
    [
        new("yt-dlp.cookies",
            Has("cookie") & HasAny("could not", "failed to decrypt", "permission", "copy"),
            "Could not read the browser's cookies: {0}",
            "Close that browser completely and try again. Chrome and Edge often block this; Firefox works best."),
        new("yt-dlp.impersonation",
            HasAny("impersonat"),
            "The site blocked the download and yt-dlp could not retry as a browser: {0}",
            "Run `outta update`, then `outta doctor` (the impersonation line must be ✓)."),
        new("yt-dlp.network",
            HasAny("unable to connect", "proxy", "timed out", "connection", "getaddrinfo", "name resolution", "network is unreachable"),
            "Network problem: {0}",
            NetworkHint),
        new("yt-dlp.geo_blocked",
            Has("geo") & HasAny("restrict", "not available in your", "country"),
            "The video is blocked in your country: {0}",
            "Try another upload of the same footage."),
        new("yt-dlp.forbidden",
            Has("no video formats found") & Has("dailymotion"),
            "Dailymotion refused the video stream (HTTP 403): {0}",
            "Try `--cookies-from-browser firefox`, or copy the video's .m3u8 link from your browser (README: 'If Dailymotion refuses a video')."),
        new("yt-dlp.unavailable",
            HasAny("private", "has been removed", "no longer available", "404", "video unavailable", "does not exist"),
            "The video is not available: {0}",
            "Check the link in a browser."),
        new("yt-dlp.login_required",
            HasAny("sign in", "login") | (Has("age") & Has("restrict")),
            "The site wants a login for this video: {0}",
            UpdateHint),
        new("yt-dlp.network",
            HasAny("unable to download", "http error 5"),
            "Network problem: {0}",
            NetworkHint),
    ];

    /// <summary>The error for yt-dlp's error output.</summary>
    public static Error Translate(string errorOutput)
    {
        string text = Clean(errorOutput);
        string lowered = text.ToLowerInvariant();
        ErrorRule? rule = Rules.FirstOrDefault(candidate => candidate.Matches.IsMatch(lowered));
        return rule is null
            ? new Error("yt-dlp.failed", $"Download failed: {text}", UpdateHint, errorOutput)
            : new Error(rule.Code, string.Format(CultureInfo.InvariantCulture, rule.Message, text), rule.Hint, errorOutput);
    }

    public static Error Missing() =>
        new("yt-dlp.missing", "yt-dlp was not found.", "Run `outta update` to download it, then try again.");

    public static Error UnreadableOutput(string problem) =>
        new("yt-dlp.unreadable", $"yt-dlp returned unreadable output: {problem}", "Run `outta update` to update yt-dlp.");

    public static Error IsPlaylist(string url) =>
        new("yt-dlp.playlist", $"{url} is a playlist.", "Use the link of a single video.");

    public static Error NoStreamAddress() =>
        new("yt-dlp.no_stream", "yt-dlp found the video but no stream address for it.", "Run `outta update` to update yt-dlp.");

    public static Error StreamRefused(IEnumerable<string> failures) =>
        new(
            "yt-dlp.stream_refused",
            "The site refused the stream: " + string.Join("; ", failures),
            "Try `--cookies-from-browser firefox`, or copy the .m3u8 link from your browser.");

    /// <summary>The last "ERROR:" line without colors and yt-dlp's "please report" boilerplate.</summary>
    public static string Clean(string errorOutput)
    {
        string text = AnsiColorRegex().Replace(errorOutput, string.Empty);
        string? errorLine = text.Split('\n').Select(line => line.Trim()).LastOrDefault(line => line.StartsWith("ERROR:", StringComparison.Ordinal));
        text = (errorLine ?? text.Trim()).Replace("ERROR: ", string.Empty, StringComparison.Ordinal);
        text = ReportBoilerplateRegex().Replace(text, string.Empty);
        return CausedByRegex().Replace(text, string.Empty).Trim();
    }

    private static TextMatcher Has(string word) => new(text => text.Contains(word, StringComparison.Ordinal));

    private static TextMatcher HasAny(params string[] words) =>
        new(text => words.Any(word => text.Contains(word, StringComparison.Ordinal)));

    [GeneratedRegex(@"\x1b\[[0-9;]*m")]
    private static partial Regex AnsiColorRegex();

    [GeneratedRegex(@";?\s*please report this issue on.*$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReportBoilerplateRegex();

    [GeneratedRegex(@"\s*\(caused by .*\)$", RegexOptions.Singleline)]
    private static partial Regex CausedByRegex();

    /// <summary>One row of the table: when it matches, use this code, message (with {0} = yt-dlp's text) and hint.</summary>
    private sealed record ErrorRule(string Code, TextMatcher Matches, string Message, string Hint);

    /// <summary>A test on lower-cased error text; combine with &amp; (both) and | (either).</summary>
    private sealed class TextMatcher(Func<string, bool> test)
    {
        public bool IsMatch(string text) => test(text);

        public static TextMatcher operator &(TextMatcher left, TextMatcher right) => new(text => left.IsMatch(text) && right.IsMatch(text));

        public static TextMatcher operator |(TextMatcher left, TextMatcher right) => new(text => left.IsMatch(text) || right.IsMatch(text));
    }
}
