using System.ComponentModel;
using System.Text.Json;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("Current episode: clips, total time, time left.")]
public sealed class EpisodeStatusCommand(EpisodeService episodes, SpectreReporter reporter, IAnsiConsole console) : Command<EpisodeNameSettings>
{
    public override int Execute(CommandContext context, EpisodeNameSettings settings, CancellationToken cancellationToken) =>
        episodes.Status(settings.Name).ToExitCode(reporter, status =>
        {
            if (settings.Json)
            {
                console.WriteLine(JsonSerializer.Serialize(EpisodeView.ToJson(status), JsonOutput.Indented));
            }
            else
            {
                EpisodeView.Write(console, status);
            }
        });
}
