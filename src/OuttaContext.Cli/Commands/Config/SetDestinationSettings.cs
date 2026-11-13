using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Config;

/// <summary>Options of `outta config set-destination FOLDER`.</summary>
public sealed class SetDestinationSettings : GlobalSettings
{
    [CommandArgument(0, "<FOLDER>")]
    [Description("Folder for clips (built episodes go in an Episodes subfolder).")]
    public string Folder { get; init; } = string.Empty;
}
