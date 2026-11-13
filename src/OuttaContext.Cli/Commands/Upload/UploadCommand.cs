using System.ComponentModel;
using System.Globalization;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Uploads;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Upload;

public sealed class UploadSettings : GlobalSettings
{
    [CommandArgument(0, "<TARGET>")]
    [Description("A file path, a clip ID or an episode name.")]
    public string Target { get; init; } = string.Empty;

    [CommandOption("--to <STORAGE>")]
    [Description("Storage name (default: the default storage).")]
    public string? To { get; init; }
}

[Description("Upload a file, a clip (by ID) or a built episode (by name).")]
public sealed class UploadCommand(UploadService uploader, ILibraryRepository repository, EpisodeService episodes) : AsyncCommand<UploadSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UploadSettings settings, CancellationToken cancellationToken)
    {
        if (File.Exists(settings.Target))
        {
            await uploader.UploadFileAsync(settings.Target, settings.To, cancellationToken);
        }
        else if (long.TryParse(settings.Target, NumberStyles.None, CultureInfo.InvariantCulture, out long clipId))
        {
            await uploader.UploadClipAsync(repository.GetClip(clipId), settings.To, cancellationToken);
        }
        else
        {
            EpisodeStatus status = episodes.Status(settings.Target);
            await uploader.UploadEpisodeAsync(status.Episode, status.Clips, settings.To, cancellationToken);
        }

        return 0;
    }
}
