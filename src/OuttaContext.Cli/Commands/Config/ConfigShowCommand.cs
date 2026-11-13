using System.ComponentModel;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

[Description("Print all settings and where the data files are.")]
public sealed class ConfigShowCommand(OuttaSettings settings, AppPaths paths, IAnsiConsole console) : Command<GlobalSettings>
{
    public override int Execute(CommandContext context, GlobalSettings options, CancellationToken cancellationToken)
    {
        var table = new Table().NoBorder().HideHeaders().AddColumns("Key", "Value", "About");
        foreach ((string key, string value) in SettingKeys.Display(settings))
        {
            table.AddRow($"[bold]{key}[/]", Markup.Escape(value), $"[dim]{Markup.Escape(SettingKeys.Descriptions[key])}[/]");
        }

        console.Write(table);
        console.MarkupLine($"\n[dim]Data folder:[/] {Markup.Escape(paths.DataDirectory)}");
        return 0;
    }
}
