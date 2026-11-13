using System.Globalization;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using Spectre.Console;

namespace OuttaContext.Cli.Commands.Episode;

/// <summary>How an episode is shown: a table of clips and a total line, or JSON.</summary>
internal static class EpisodeView
{
    public static string StateText(Core.Library.Episode episode) => episode.IsBuilt ? "built" : "open";

    public static void Write(IAnsiConsole console, EpisodeStatus status)
    {
        string state = status.Episode.IsBuilt ? "[green]built[/]" : "[cyan]open[/]";
        console.MarkupLine($"[bold]{Markup.Escape(status.Episode.Name.Value)}[/] ({state})");
        if (status.Clips.Count == 0)
        {
            console.MarkupLine("  [dim]No clips yet.[/]");
        }
        else
        {
            var table = new Table().NoBorder().AddColumns("#", "ID", "Title", "Length", "Source");
            foreach (Clip clip in status.Clips)
            {
                string missing = File.Exists(clip.FilePath) ? string.Empty : " [red](file missing)[/]";
                table.AddRow(
                    clip.Position.Map(position => position.ToString(CultureInfo.InvariantCulture)).GetValueOrDefault(string.Empty),
                    clip.Id.ToString(CultureInfo.InvariantCulture),
                    Markup.Escape(clip.Title.Value) + missing,
                    clip.Duration.ToClock(),
                    Markup.Escape(clip.Source));
            }

            console.Write(table);
        }

        string total = $"Total {status.Total} of {status.Episode.TargetLength}";
        console.MarkupLine(status.IsReady
            ? $"{total} - [green]ready to build[/] (`outta episode build`)"
            : $"{total} - {status.Remaining} left");
        status.Episode.OutputPath.Tap(output => console.MarkupLine($"Video: {Markup.Escape(output)}"));
    }

    /// <summary>The --json shape (unchanged from the Python version: lengths in seconds).</summary>
    public static object ToJson(EpisodeStatus status) => new
    {
        name = status.Episode.Name.Value,
        state = StateText(status.Episode),
        target = status.Episode.TargetLength.Seconds,
        total = Math.Round(status.Total.Seconds, 2),
        remaining = Math.Round(status.Remaining.Seconds, 2),
        output = status.Episode.OutputPath.GetValueOrDefault(),
        clips = status.Clips.Select(clip => new
        {
            id = clip.Id,
            position = clip.Position.HasValue ? clip.Position.Value : (int?)null,
            title = clip.Title.Value,
            duration = Math.Round(clip.Duration.Seconds, 2),
            source = clip.Source,
            file = clip.FilePath,
            exists = File.Exists(clip.FilePath),
        }),
    };
}
