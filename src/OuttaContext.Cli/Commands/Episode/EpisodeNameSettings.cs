using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode status [NAME]`.</summary>
public sealed class EpisodeNameSettings : GlobalSettings
{
    [CommandArgument(0, "[NAME]")]
    [Description("Episode name (default: the current episode).")]
    public string? Name { get; init; }

    [CommandOption("--json")]
    [Description("Print as JSON.")]
    public bool Json { get; init; }
}
