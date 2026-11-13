using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode list`.</summary>
public sealed class EpisodeListSettings : GlobalSettings
{
    [CommandOption("--json")]
    [Description("Print as JSON.")]
    public bool Json { get; init; }
}
