using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Storage;

/// <summary>Options of `outta storage test|default|remove NAME`.</summary>
public sealed class StorageNameSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    [Description("Storage name.")]
    public string Name { get; init; } = string.Empty;
}
