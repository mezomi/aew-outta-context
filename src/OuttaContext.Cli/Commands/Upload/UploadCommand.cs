using System.ComponentModel;
using System.Globalization;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Uploads;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Upload;

/// <summary>`outta upload TARGET`: TARGET is tried as a file path, then a clip ID or title, then an episode name.</summary>
[Description("Upload a file, a clip (by ID or title) or a built episode (by name).")]
public sealed class UploadCommand(UploadService uploader, ILibraryRepository repository, EpisodeService episodes, SpectreReporter reporter)
    : AsyncCommand<UploadSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, UploadSettings settings, CancellationToken cancellationToken)
    {
        string target = settings.Target;
        if (File.Exists(target))
        {
            return await uploader.UploadFileAsync(target, settings.To, cancellationToken).ToExitCode(reporter);
        }

        if (FindClip(target).TryGetValue(out Clip? clip))
        {
            return await uploader.UploadClipAsync(clip, settings.To, cancellationToken).ToExitCode(reporter);
        }

        Result<EpisodeStatus, Error> episode = episodes.Status(target).MapError(_ => UploadErrors.NothingMatches(target));
        if (episode.IsFailure)
        {
            return episode.ToExitCode(reporter);
        }

        return await uploader.UploadEpisodeAsync(episode.Value.Episode, episode.Value.Clips, settings.To, cancellationToken).ToExitCode(reporter);
    }

    private Maybe<Clip> FindClip(string idOrTitle) =>
        long.TryParse(idOrTitle, NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            ? repository.FindClip(id)
            : ClipTitle.Create(idOrTitle).ValueOrNone().Bind(repository.FindClipByTitle);
}
