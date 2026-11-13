using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OuttaContext.Core.Clips;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Time;
using OuttaContext.Core.Uploads;

namespace OuttaContext.Core.Episodes;

public sealed record BuildEpisodeRequest(string? Name = null, bool Upload = false, string? Storage = null, bool CleanLocal = false);

public sealed record BuildEpisodeResult(Episode Episode, string OutputPath, double Duration, Episode? NextEpisode);

/// <summary>
/// `outta episode build`: convert every clip to the same format → join them → mark the episode
/// built → open the next one → upload (optional). Converted clips are cached, so rebuilding after
/// a reorder only joins.
/// </summary>
public sealed class BuildEpisodeHandler(
    EpisodeService episodes,
    IMediaProcessor media,
    OuttaSettings settings,
    string cacheDirectory,
    IReporter reporter,
    IEpisodeUploader? uploader = null,
    NormalizeTarget? target = null)
{
    private readonly NormalizeTarget _target = target ?? new NormalizeTarget();

    public async Task<BuildEpisodeResult> HandleAsync(BuildEpisodeRequest request, CancellationToken cancellationToken)
    {
        EpisodeStatus status = episodes.Status(request.Name);
        Episode episode = status.Episode;
        IReadOnlyList<Clip> clips = status.Clips;
        if (clips.Count == 0)
        {
            throw new EpisodeEmptyException($"\"{episode.Name}\" has no clips.");
        }

        if (clips.FirstOrDefault(clip => !File.Exists(clip.FilePath)) is { } missing)
        {
            throw new ClipFileMissingException($"Clip {missing.Id} (\"{missing.Title}\") is missing its file: {missing.FilePath}");
        }

        if (request.CleanLocal && !request.Upload)
        {
            throw new OuttaException("--clean-local only works with --upload.", "Add --upload.");
        }

        if (!media.IsAvailable)
        {
            throw new OuttaException("ffmpeg was not found.", "Install it: `winget install ffmpeg`, then open a new terminal.");
        }

        reporter.Step($"Building \"{episode.Name}\": {clips.Count} clips, {TimeText.Format(status.Total)}");
        var converted = new List<string>();
        for (int index = 0; index < clips.Count; index++)
        {
            Clip clip = clips[index];
            string cached = CachePath(clip);
            string label = string.Create(CultureInfo.InvariantCulture, $"{index + 1}/{clips.Count} \"{clip.Title}\"");
            if (File.Exists(cached))
            {
                reporter.Step($"Clip {label}: using converted copy");
            }
            else
            {
                reporter.Step($"Converting clip {label}");
                (await media.NormalizeAsync(clip.FilePath, cached, _target, cancellationToken)).ValueOrThrow();
            }

            converted.Add(cached);
        }

        string output = Path.Combine(settings.EpisodesFolder, ClipTitle.ToFileName(episode.Name) + ".mp4");
        reporter.Step("Joining clips");
        (await media.ConcatAsync(converted, output, Path.Combine(cacheDirectory, "work"), cancellationToken)).ValueOrThrow();
        double duration = (await media.ProbeAsync(output, cancellationToken)).ValueOrThrow().Duration;
        (Episode built, Episode? opened) = episodes.Finish(episode, output);
        reporter.Success($"Built {output} ({TimeText.Format(duration)})");
        if (opened is not null)
        {
            reporter.Success($"Opened \"{opened.Name}\" for new clips");
        }

        if (request.Upload)
        {
            if (uploader is null)
            {
                throw new OuttaException("Uploading is not available.");
            }

            await uploader.UploadEpisodeAsync(built, clips, request.Storage, cancellationToken);
            if (request.CleanLocal)
            {
                CleanLocal(clips, converted);
            }
        }

        return new BuildEpisodeResult(built, output, duration, opened);
    }

    /// <summary>A cached copy's name changes when the clip file or the target format changes.</summary>
    public string CachePath(Clip clip)
    {
        var file = new FileInfo(clip.FilePath);
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{_target.CacheKey}");
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
        return Path.Combine(cacheDirectory, "normalized", string.Create(CultureInfo.InvariantCulture, $"clip{clip.Id}-{digest}.mp4"));
    }

    private void CleanLocal(IReadOnlyList<Clip> clips, IReadOnlyList<string> converted)
    {
        foreach (string path in clips.Select(clip => clip.FilePath).Concat(converted))
        {
            File.Delete(path);
        }

        reporter.Success($"Deleted {clips.Count} local clip files (they are uploaded)");
    }
}
