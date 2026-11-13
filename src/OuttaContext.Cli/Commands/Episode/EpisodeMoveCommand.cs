using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("Move a clip to another position in its episode.")]
public sealed class EpisodeMoveCommand(EpisodeService episodes, SpectreReporter reporter, IAnsiConsole console) : Command<EpisodeMoveSettings>
{
    public override int Execute(CommandContext context, EpisodeMoveSettings settings, CancellationToken cancellationToken) =>
        episodes.Move(settings.ClipId, settings.Position).ToExitCode(reporter, status =>
        {
            reporter.Success($"Moved clip {settings.ClipId} to position {settings.Position}");
            EpisodeView.Write(console, status);
        });
}
