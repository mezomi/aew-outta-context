using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Upload;

/// <summary>Options of `outta upload TARGET`.</summary>
public sealed class UploadSettings : GlobalSettings
{
    [CommandArgument(0, "<TARGET>")]
    [Description("A file path, a clip ID or an episode name.")]
    public string Target { get; init; } = string.Empty;

    [CommandOption("--to <STORAGE>")]
    [Description("Storage name (default: the default storage).")]
    public string? To { get; init; }
}
