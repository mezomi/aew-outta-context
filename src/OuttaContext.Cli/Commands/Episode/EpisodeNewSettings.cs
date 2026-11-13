using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode new NAME`.</summary>
public sealed class EpisodeNewSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    [Description("e.g. \"All Out special\".")]
    public string Name { get; init; } = string.Empty;
}
