using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("Take a clip out of its episode (the file is kept).")]
public sealed class EpisodeRemoveCommand(EpisodeService episodes, SpectreReporter reporter) : Command<EpisodeRemoveSettings>
{
    public override int Execute(CommandContext context, EpisodeRemoveSettings settings, CancellationToken cancellationToken) =>
        episodes.Remove(settings.ClipId).ToExitCode(reporter, removed =>
            reporter.Success($"Removed \"{removed.Clip.Title}\" from \"{removed.Status.Episode.Name}\" (file kept: {removed.Clip.FilePath})"));
}
