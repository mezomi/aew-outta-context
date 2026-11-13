using System.ComponentModel;
using OuttaContext.Core.Common;
using OuttaContext.Core.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Clips;

/// <summary>Options of `outta clip`.</summary>
public sealed class ClipSettings : GlobalSettings
{
    [CommandArgument(0, "<URL>")]
    [Description("Dailymotion or YouTube video link, or a direct .m3u8/.mp4 link.")]
    public string Url { get; init; } = string.Empty;

    [CommandOption("-r|--range <START-END>")]
    [Description("Required. START-END: 90-112, 1:30-1:52 or 1:02:10-1:02:30.")]
    public string? Range { get; init; }

    [CommandOption("-t|--title <TITLE>")]
    [Description("Required. Clip title (unique); becomes the file name.")]
    public string? Title { get; init; }

    [CommandOption("-e|--episode <NAME>")]
    [Description("Add to this episode instead of the current one.")]
    public string? Episode { get; init; }

    [CommandOption("--no-episode")]
    [Description("Don't add the clip to any episode.")]
    public bool NoEpisode { get; init; }

    [CommandOption("--upload")]
    [Description("Upload to the default storage afterwards.")]
    public bool Upload { get; init; }

    [CommandOption("--to <STORAGE>")]
    [Description("Storage for --upload.")]
    public string? To { get; init; }

    [CommandOption("--referer <PAGE_URL>")]
    [Description("The page the video is embedded on (for embed-only videos).")]
    public string? Referer { get; init; }

    [CommandOption("--cookies-from-browser <BROWSER>")]
    [Description("Use this browser's cookies (firefox, edge, chrome). Helps when a site refuses.")]
    public string? CookiesFromBrowser { get; init; }

    [CommandOption("--cookies <FILE>")]
    [Description("A cookies.txt file (Netscape format) exported from your browser.")]
    public string? Cookies { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return ValidationResult.Error("Missing --title. Every clip needs a title, e.g. --title \"Table spot\".");
        }

        if (string.IsNullOrWhiteSpace(Range))
        {
            return ValidationResult.Error("Missing --range, e.g. --range 1:30-1:52.");
        }

        if (CookiesFromBrowser is not null && BrowserName.Parse(CookiesFromBrowser).TryGetError(out Error? browserError))
        {
            return ValidationResult.Error(browserError.Message + " " + browserError.Hint);
        }

        if (Episode is not null && NoEpisode)
        {
            return ValidationResult.Error("Use --episode or --no-episode, not both.");
        }

        return base.Validate();
    }
}
