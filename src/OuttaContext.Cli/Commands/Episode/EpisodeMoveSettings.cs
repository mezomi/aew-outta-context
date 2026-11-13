using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode move CLIP_ID POSITION`.</summary>
public sealed class EpisodeMoveSettings : GlobalSettings
{
    [CommandArgument(0, "<CLIP_ID>")]
    [Description("Clip ID (see `outta episode status`).")]
    public long ClipId { get; init; }

    [CommandArgument(1, "<POSITION>")]
    [Description("New position, starting at 1.")]
    public int Position { get; init; }
}
