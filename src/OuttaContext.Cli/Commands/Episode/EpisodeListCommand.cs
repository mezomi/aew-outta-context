using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using OuttaContext.Core.Episodes;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

[Description("All episodes and their state.")]
public sealed class EpisodeListCommand(EpisodeService episodes, IAnsiConsole console) : Command<EpisodeListSettings>
{
    public override int Execute(CommandContext context, EpisodeListSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<EpisodeStatus> statuses = episodes.AllStatuses();
        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(statuses.Select(EpisodeView.ToJson), JsonOutput.Indented));
            return 0;
        }

        long currentId = episodes.Current().Id;
        var table = new Table().NoBorder().AddColumns("Episode", "State", "Clips", "Length", "Video");
        foreach (EpisodeStatus status in statuses)
        {
            string marker = status.Episode.Id == currentId ? " [cyan]← current[/]" : string.Empty;
            table.AddRow(
                Markup.Escape(status.Episode.Name.Value) + marker,
                EpisodeView.StateText(status.Episode),
                status.Clips.Count.ToString(CultureInfo.InvariantCulture),
                $"{status.Total} / {status.Episode.TargetLength}",
                Markup.Escape(status.Episode.OutputPath.GetValueOrDefault(string.Empty)));
        }

        console.Write(table);
        return 0;
    }
}
