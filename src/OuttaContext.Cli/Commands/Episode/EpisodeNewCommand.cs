using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("Start a new episode by hand; new clips go into it.")]
public sealed class EpisodeNewCommand(EpisodeService episodes, SpectreReporter reporter) : Command<EpisodeNewSettings>
{
    public override int Execute(CommandContext context, EpisodeNewSettings settings, CancellationToken cancellationToken) =>
        episodes.StartNew(settings.Name)
            .ToExitCode(reporter, episode => reporter.Success($"Started \"{episode.Name}\" ({episode.TargetLength} target)"));
}
