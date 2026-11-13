using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

[Description("Set the storage used by --upload and `outta upload`.")]
public sealed class StorageDefaultCommand(IStorageRegistry storages, ISettingsStore settingsStore, SpectreReporter reporter) : Command<StorageNameSettings>
{
    public override int Execute(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken) =>
        StorageName.Create(options.Name)
            .Bind(storages.Get)
            .Bind(config => settingsStore.Change(SettingKeys.DefaultStorage, config.Name.Value).Map(_ => config))
            .ToExitCode(reporter, config => reporter.Success($"{config.Name} is now the default storage"));
}
