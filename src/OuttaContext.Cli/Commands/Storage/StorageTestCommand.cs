using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

[Description("Check a storage login works (uploads and deletes a tiny file).")]
public sealed class StorageTestCommand(IStorageRegistry storages, IStorageBackendFactory backends, SpectreReporter reporter)
    : AsyncCommand<StorageNameSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, StorageNameSettings options, CancellationToken cancellationToken) =>
        StorageName.Create(options.Name)
            .Bind(storages.Get)
            .Tap(config => reporter.Step($"Testing {config.Name}"))
            .Bind(async config => (await backends.Create(config).TestAsync(cancellationToken)).Map(() => config))
            .ToExitCode(reporter, config => reporter.Success($"{config.Name} works"));
}
