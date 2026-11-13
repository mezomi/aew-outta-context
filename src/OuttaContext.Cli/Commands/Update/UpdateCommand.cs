using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using OuttaContext.Infrastructure.Updates;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Update;

[Description("Update outta.exe and yt-dlp to the newest builds (your data is kept).")]
public sealed class UpdateCommand(SelfUpdater selfUpdater, YtDlpInstaller ytDlp, SpectreReporter reporter) : AsyncCommand<UpdateSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UpdateSettings settings, CancellationToken cancellationToken)
    {
        if (settings.YtDlpOnly)
        {
            return await ytDlp.InstallOrUpdateAsync(cancellationToken).ToExitCode(reporter);
        }

        UnitResult<Error> self = UnitResult.Success<Error>();
        if (RunningExecutable.IsPublished)
        {
            self = (await selfUpdater.UpdateAsync(settings.Check, cancellationToken)).DropValue();
        }
        else
        {
            reporter.Warn("This is a development build; outta.exe itself is not updated. Pull and rebuild instead.");
        }

        if (self.IsFailure || settings.Check)
        {
            return self.ToExitCode(reporter);
        }

        return await ytDlp.InstallOrUpdateAsync(cancellationToken).ToExitCode(reporter);
    }
}
