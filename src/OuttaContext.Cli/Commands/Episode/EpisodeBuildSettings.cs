using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>Options of `outta episode build [NAME]`.</summary>
public sealed class EpisodeBuildSettings : GlobalSettings
{
    [CommandArgument(0, "[NAME]")]
    [Description("Episode name (default: the current episode).")]
    public string? Name { get; init; }

    [CommandOption("--upload")]
    [Description("Upload the episode and its clips.")]
    public bool Upload { get; init; }

    [CommandOption("--to <STORAGE>")]
    [Description("Storage for --upload.")]
    public string? To { get; init; }

    [CommandOption("--clean-local")]
    [Description("With --upload: delete local clip files once uploaded.")]
    public bool CleanLocal { get; init; }
}
