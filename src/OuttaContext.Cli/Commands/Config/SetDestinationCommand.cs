using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Settings;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

[Description("Where clips (and built episodes) are saved.")]
public sealed class SetDestinationCommand(ISettingsStore store, SpectreReporter reporter) : Command<SetDestinationSettings>
{
    public override int Execute(CommandContext context, SetDestinationSettings settings, CancellationToken cancellationToken)
    {
        string folder = Path.GetFullPath(settings.Folder);
        return store.Change(SettingKeys.Destination, folder)
            .Tap(_ => Directory.CreateDirectory(folder))
            .ToExitCode(reporter, _ => reporter.Success($"Clips will be saved in {folder}"));
    }
}
