using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

/// <summary>Options of `outta config set KEY VALUE`.</summary>
public sealed class ConfigSetSettings : GlobalSettings
{
    [CommandArgument(0, "<KEY>")]
    [Description("destination, episode-length, max-clip-length, max-quality, default-storage or cookies-from-browser.")]
    public string Key { get; init; } = string.Empty;

    [CommandArgument(1, "<VALUE>")]
    [Description("The new value, e.g. 10:00, 45, off, 1080, firefox.")]
    public string Value { get; init; } = string.Empty;
}
