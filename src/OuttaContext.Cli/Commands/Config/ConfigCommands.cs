using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

public sealed class SetDestinationSettings : GlobalSettings
{
    [CommandArgument(0, "<FOLDER>")]
    [Description("Folder for clips (built episodes go in an Episodes subfolder).")]
    public string Folder { get; init; } = string.Empty;
}

public sealed class ConfigSetSettings : GlobalSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("destination, episode-length, max-clip-length, max-quality, default-storage or cookies-from-browser.")]
    public string Key { get; init; } = string.Empty;

    [CommandArgument(1, "<VALUE>")]
    [Description("The new value, e.g. 10:00, 45, off, 1080, firefox.")]
    public string Value { get; init; } = string.Empty;
}

[Description("Where clips (and built episodes) are saved.")]
public sealed class SetDestinationCommand(ISettingsStore store, SpectreReporter reporter) : Command<SetDestinationSettings>
{
    public override int Execute(CommandContext context, SetDestinationSettings settings, CancellationToken cancellationToken)
    {
        string folder = Path.GetFullPath(settings.Folder);
        Directory.CreateDirectory(folder);
        store.Set(SettingKeys.Destination, folder);
        reporter.Success($"Clips will be saved in {folder}");
        return 0;
    }
}

[Description("Change a setting, e.g. `outta config set episode-length 10:00`.")]
public sealed class ConfigSetCommand(ISettingsStore store, SpectreReporter reporter) : Command<ConfigSetSettings>
{
    public override int Execute(CommandContext context, ConfigSetSettings settings, CancellationToken cancellationToken)
    {
        OuttaSettings updated = store.Set(settings.Key, settings.Value);
        string key = settings.Key.ToLowerInvariant().Replace('_', '-');
        string shown = SettingKeys.Display(updated).TryGetValue(key, out string? value) ? value : settings.Value;
        reporter.Success($"{settings.Key} = {shown}");
        return 0;
    }
}

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
