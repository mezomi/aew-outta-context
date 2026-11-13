using System.ComponentModel;
using System.Text.Json;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Time;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Episode;

public sealed class EpisodeNameSettings : GlobalSettings
{
    [CommandArgument(0, "[NAME]")]
    [Description("Episode name (default: the current episode).")]
    public string? Name { get; init; }

    [CommandOption("--json")]
    [Description("Print as JSON.")]
    public bool Json { get; init; }
}

public sealed class EpisodeListSettings : GlobalSettings
{
    [CommandOption("--json")]
    [Description("Print as JSON.")]
    public bool Json { get; init; }
}

public sealed class EpisodeNewSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    [Description("e.g. \"All Out special\".")]
    public string Name { get; init; } = string.Empty;
}

public sealed class EpisodeMoveSettings : GlobalSettings
{
    [CommandArgument(0, "<CLIP_ID>")]
    [Description("Clip ID (see `outta episode status`).")]
    public long ClipId { get; init; }

    [CommandArgument(1, "<POSITION>")]
    [Description("New position, starting at 1.")]
    public int Position { get; init; }
}

public sealed class EpisodeRemoveSettings : GlobalSettings
{
    [CommandArgument(0, "<CLIP_ID>")]
    [Description("Clip ID.")]
    public long ClipId { get; init; }
}

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

[Description("Current episode: clips, total time, time left.")]
public sealed class EpisodeStatusCommand(EpisodeService episodes, IAnsiConsole console) : Command<EpisodeNameSettings>
{
    public override int Execute(CommandContext context, EpisodeNameSettings settings, CancellationToken cancellationToken)
    {
        EpisodeStatus status = episodes.Status(settings.Name);
        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(EpisodeView.ToJson(status), JsonOutput.Indented));
        }
        else
        {
            EpisodeView.Write(console, status);
        }

        return 0;
    }
}

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
                Markup.Escape(status.Episode.Name) + marker,
                status.Episode.State,
                status.Clips.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"{TimeText.Format(status.Total)} / {TimeText.Format(status.Episode.TargetLength)}",
                Markup.Escape(status.Episode.OutputPath ?? string.Empty));
        }

        console.Write(table);
        return 0;
    }
}

[Description("Start a new episode by hand; new clips go into it.")]
public sealed class EpisodeNewCommand(EpisodeService episodes, SpectreReporter reporter) : Command<EpisodeNewSettings>
{
    public override int Execute(CommandContext context, EpisodeNewSettings settings, CancellationToken cancellationToken)
    {
        var episode = episodes.StartNew(settings.Name);
        reporter.Success($"Started \"{episode.Name}\" ({TimeText.Format(episode.TargetLength)} target)");
        return 0;
    }
}

[Description("Move a clip to another position in its episode.")]
public sealed class EpisodeMoveCommand(EpisodeService episodes, SpectreReporter reporter, IAnsiConsole console) : Command<EpisodeMoveSettings>
{
    public override int Execute(CommandContext context, EpisodeMoveSettings settings, CancellationToken cancellationToken)
    {
        EpisodeStatus status = episodes.Move(settings.ClipId, settings.Position);
        reporter.Success($"Moved clip {settings.ClipId} to position {settings.Position}");
        EpisodeView.Write(console, status);
        return 0;
    }
}

[Description("Take a clip out of its episode (the file is kept).")]
public sealed class EpisodeRemoveCommand(EpisodeService episodes, SpectreReporter reporter) : Command<EpisodeRemoveSettings>
{
    public override int Execute(CommandContext context, EpisodeRemoveSettings settings, CancellationToken cancellationToken)
    {
        (Clip clip, EpisodeStatus status) = episodes.Remove(settings.ClipId);
        reporter.Success($"Removed \"{clip.Title}\" from \"{status.Episode.Name}\" (file kept: {clip.FilePath})");
        return 0;
    }
}

[Description("Join the episode's clips into one MP4, then open the next episode.")]
public sealed class EpisodeBuildCommand(BuildEpisodeHandler handler) : AsyncCommand<EpisodeBuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, EpisodeBuildSettings settings, CancellationToken cancellationToken)
    {
        var request = new BuildEpisodeRequest(settings.Name, settings.Upload || settings.To is not null, settings.To, settings.CleanLocal);
        await handler.HandleAsync(request, cancellationToken);
        return 0;
    }
}

/// <summary>How an episode is shown: a table of clips and a total line.</summary>
internal static class EpisodeView
{
    public static void Write(IAnsiConsole console, EpisodeStatus status)
    {
        string state = status.Episode.IsBuilt ? "[green]built[/]" : "[cyan]open[/]";
        console.MarkupLine($"[bold]{Markup.Escape(status.Episode.Name)}[/] ({state})");
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
                    clip.Position?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    clip.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Markup.Escape(clip.Title) + missing,
                    TimeText.Format(clip.Duration),
                    Markup.Escape(clip.Source));
            }

            console.Write(table);
        }

        string total = $"Total {TimeText.Format(status.Total)} of {TimeText.Format(status.Episode.TargetLength)}";
        console.MarkupLine(status.IsReady
            ? $"{total} - [green]ready to build[/] (`outta episode build`)"
            : $"{total} - {TimeText.Format(status.Remaining)} left");
        if (status.Episode.OutputPath is { } output)
        {
            console.MarkupLine($"Video: {Markup.Escape(output)}");
        }
    }

    public static object ToJson(EpisodeStatus status) => new
    {
        name = status.Episode.Name,
        state = status.Episode.State,
        target = status.Episode.TargetLength,
        total = Math.Round(status.Total, 2),
        remaining = Math.Round(status.Remaining, 2),
        output = status.Episode.OutputPath,
        clips = status.Clips.Select(clip => new
        {
            id = clip.Id,
            position = clip.Position,
            title = clip.Title,
            duration = Math.Round(clip.Duration, 2),
            source = clip.Source,
            file = clip.FilePath,
            exists = File.Exists(clip.FilePath),
        }),
    };
}
