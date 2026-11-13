using OuttaContext.Core.Common;

namespace OuttaContext.Core.Settings;

/// <summary>
/// A browser whose login cookies yt-dlp may use: <c>firefox</c>, <c>edge</c>, ... optionally with a
/// profile after a colon (<c>firefox:work</c>), the way yt-dlp's --cookies-from-browser takes it.
/// </summary>
public sealed class BrowserName : SimpleValueObject<string>
{
    public static readonly IReadOnlyList<string> Supported =
        ["firefox", "edge", "chrome", "brave", "opera", "vivaldi", "chromium", "safari"];

    private BrowserName(string value)
        : base(value)
    {
    }

    public static Result<BrowserName, Error> Parse(string text)
    {
        string lowered = text.Trim().ToLowerInvariant();
        return Supported.Contains(lowered.Split(':')[0])
            ? new BrowserName(lowered)
            : SettingsErrors.UnsupportedBrowser(text);
    }

    public override string ToString() => Value;
}
