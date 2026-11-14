using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Settings;
using OuttaContext.Infrastructure.YtDlp;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Clips;

/// <summary>`outta clip URL --range START-END --title TITLE`</summary>
[Description("Download a titled clip and add it to the current episode.")]
public sealed class ClipCommand(MakeClipHandler handler, YtDlpOptions ytDlp, SpectreReporter reporter) : AsyncCommand<ClipSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ClipSettings settings, CancellationToken cancellationToken)
    {
        if (settings.CookiesFromBrowser is not null)
        {
            // Already checked in ClipSettings.Validate.
            ytDlp.CookiesFromBrowser = BrowserName.Parse(settings.CookiesFromBrowser).Value;
        }

        ytDlp.CookieFile = Maybe.From(settings.Cookies);
        var request = new MakeClipRequest(
            settings.Url,
            settings.Range!,
            settings.Title!,
            settings.Episode,
            settings.NoEpisode,
            Upload: settings.Upload || settings.To is not null,
            Storage: settings.To,
            Referer: settings.Referer)
        {
            Replace = settings.Replacement,
        };
        return handler.HandleAsync(request, cancellationToken).ToExitCode(reporter);
    }
}
