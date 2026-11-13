using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode remove CLIP_ID`.</summary>
public sealed class EpisodeRemoveSettings : GlobalSettings
{
    [CommandArgument(0, "<CLIP_ID>")]
    [Description("Clip ID.")]
    public long ClipId { get; init; }
}
