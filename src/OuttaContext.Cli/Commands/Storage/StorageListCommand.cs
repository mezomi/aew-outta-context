using System.ComponentModel;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

[Description("Show configured storages.")]
public sealed class StorageListCommand(IStorageRegistry storages, OuttaSettings settings, IAnsiConsole console) : Command<GlobalSettings>
{
    public override int Execute(CommandContext context, GlobalSettings options, CancellationToken cancellationToken)
    {
        IReadOnlyList<StorageConfig> configs = storages.List();
        if (configs.Count == 0)
        {
            console.WriteLine("No storage yet. Add one with `outta storage add drive --provider gdrive`.");
            return 0;
        }

        var table = new Table().NoBorder().AddColumns("Name", "Provider", "Folder");
        foreach (StorageConfig config in configs)
        {
            string marker = settings.DefaultStorage == config.Name ? " [cyan](default)[/]" : string.Empty;
            table.AddRow(Markup.Escape(config.Name.Value) + marker, config.Provider.Label, Markup.Escape(config.Folder));
        }

        console.Write(table);
        return 0;
    }
}
