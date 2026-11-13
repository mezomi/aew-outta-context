using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Infrastructure.Updates;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Update;

public sealed class UpdateSettings : GlobalSettings
{
    [CommandOption("--check")]
    [Description("Only say whether an update is available.")]
    public bool Check { get; init; }

    [CommandOption("--yt-dlp-only")]
    [Description("Only install or update yt-dlp; leave outta.exe as it is.")]
    public bool YtDlpOnly { get; init; }
}

[Description("Update outta.exe and yt-dlp to the newest builds (your data is kept).")]
public sealed class UpdateCommand(SelfUpdater selfUpdater, YtDlpInstaller ytDlp, SpectreReporter reporter) : AsyncCommand<UpdateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings, CancellationToken cancellationToken)
    {
        if (settings.YtDlpOnly)
        {
            (await ytDlp.InstallOrUpdateAsync(cancellationToken)).ValueOrThrow();
            return 0;
        }

        if (SelfUpdater.IsPublishedExecutable)
        {
            (await selfUpdater.UpdateAsync(settings.Check, cancellationToken)).ValueOrThrow();
        }
        else
        {
            reporter.Warn("This is a development build; outta.exe itself is not updated. Pull and rebuild instead.");
        }

        if (!settings.Check)
        {
            (await ytDlp.InstallOrUpdateAsync(cancellationToken)).ValueOrThrow();
        }

        return 0;
    }
}
