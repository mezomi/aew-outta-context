using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

[Description("Remove a storage, its login and any stored key.")]
public sealed class StorageRemoveCommand(
    IStorageRegistry storages, IStorageBackendFactory backends, ISettingsStore settingsStore, OuttaSettings settings, SpectreReporter reporter)
    : AsyncCommand<StorageNameSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken) =>
        StorageName.Create(options.Name)
            .Bind(storages.Get)
            .Bind(async config => (await backends.Create(config).ForgetAsync(cancellationToken)).Map(() => config))
            .Bind(config => storages.Remove(config.Name))
            .Bind(ClearDefaultIfRemoved)
            .ToExitCode(reporter, config => reporter.Success($"Removed storage {config.Name}"));

    private Result<StorageConfig, Error> ClearDefaultIfRemoved(StorageConfig removed) =>
        settings.DefaultStorage == removed.Name
            ? settingsStore.Change(SettingKeys.DefaultStorage, string.Empty).Map(_ => removed)
            : removed;
}
