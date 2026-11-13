using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Settings;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

[Description("Change a setting, e.g. `outta config set episode-length 10:00`.")]
public sealed class ConfigSetCommand(ISettingsStore store, SpectreReporter reporter) : Command<ConfigSetSettings>
{
    public override int Execute(CommandContext context, ConfigSetSettings settings, CancellationToken cancellationToken) =>
        store.Change(settings.Key, settings.Value).ToExitCode(reporter, updated =>
        {
            string key = settings.Key.ToLowerInvariant().Replace('_', '-');
            string shown = SettingKeys.Display(updated).TryGetValue(key, out string? value) ? value : settings.Value;
            reporter.Success($"{settings.Key} = {shown}");
        });
}
