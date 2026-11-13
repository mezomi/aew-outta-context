using System.ComponentModel;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("Join the episode's clips into one MP4, then open the next episode.")]
public sealed class EpisodeBuildCommand(BuildEpisodeHandler handler, SpectreReporter reporter) : AsyncCommand<EpisodeBuildSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, EpisodeBuildSettings settings, CancellationToken cancellationToken)
    {
        var request = new BuildEpisodeRequest(settings.Name, settings.Upload || settings.To is not null, settings.To, settings.CleanLocal);
        return handler.HandleAsync(request, cancellationToken).ToExitCode(reporter);
    }
}
