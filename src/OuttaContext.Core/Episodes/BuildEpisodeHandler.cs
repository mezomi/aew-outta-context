using System.Globalization;
using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Time;
using OuttaContext.Core.Uploads;

namespace OuttaContext.Core.Episodes;

/// <summary>
/// `outta episode build`: check the episode → convert every clip to the same format → join them →
/// mark the episode built and open the next one → upload (optional).
/// </summary>
public sealed class BuildEpisodeHandler(
    EpisodeService episodes,
    IMediaProcessor media,
    EpisodeCache cache,
    OuttaSettings settings,
    IEpisodeUploader uploader,
    IReporter reporter)
{
    public Task<Result<BuildEpisodeResult, Error>> HandleAsync(BuildEpisodeRequest request, CancellationToken cancellationToken) =>
        Check(request)
            .Bind(status => ConvertClipsAsync(status, cancellationToken).Map(converted => (status, converted)))
            .Bind(job => JoinAsync(job.status, job.converted, cancellationToken))
            .Bind(built => UploadAsync(built, request, cancellationToken));

    /// <summary>Step 1: the episode exists, has clips, every clip file is there, and ffmpeg is installed.</summary>
    private Result<EpisodeStatus, Error> Check(BuildEpisodeRequest request) =>
        UnitResult.SuccessIf(!request.CleanLocal || request.Upload, EpisodeErrors.CleanLocalNeedsUpload())
            .Bind(() => episodes.Status(request.Name))
            .Ensure(status => status.Clips.Count > 0, status => EpisodeErrors.Empty(status.Episode))
            .Ensure(status => status.Clips.All(clip => File.Exists(clip.FilePath)),
                status => EpisodeErrors.ClipFileMissing(status.Clips.First(clip => !File.Exists(clip.FilePath))))
            .Ensure(_ => media.IsAvailable, MediaErrors.FfmpegMissing());

    /// <summary>Step 2: convert each clip (or reuse its converted copy).</summary>
    private async Task<Result<IReadOnlyList<string>, Error>> ConvertClipsAsync(EpisodeStatus status, CancellationToken cancellationToken)
    {
        IReadOnlyList<Clip> clips = status.Clips;
        reporter.Step($"Building \"{status.Episode.Name}\": {clips.Count} clips, {status.Total}");
        var converted = new List<string>();
        for (int index = 0; index < clips.Count; index++)
        {
            Clip clip = clips[index];
            string cached = cache.PathFor(clip);
            string label = string.Create(CultureInfo.InvariantCulture, $"{index + 1}/{clips.Count} \"{clip.Title}\"");
            if (File.Exists(cached))
            {
                reporter.Step($"Clip {label}: using converted copy");
            }
            else
            {
                reporter.Step($"Converting clip {label}");
                UnitResult<Error> normalized = await media.NormalizeAsync(clip.FilePath, cached, cache.Target, cancellationToken);
                if (normalized.IsFailure)
                {
                    return normalized.Error;
                }
            }

            converted.Add(cached);
        }

        return converted;
    }

    /// <summary>Step 3: join the converted clips, then mark the episode built.</summary>
    private async Task<Result<BuiltEpisode, Error>> JoinAsync(
        EpisodeStatus status, IReadOnlyList<string> converted, CancellationToken cancellationToken)
    {
        string output = Path.Combine(settings.EpisodesFolder, status.Episode.Name.FileName + ".mp4");
        reporter.Step("Joining clips");
        UnitResult<Error> joined = await media.ConcatAsync(converted, output, cache.WorkDirectory, cancellationToken);
        if (joined.IsFailure)
        {
            return joined.Error;
        }

        Result<ProbeResult, Error> probe = await media.ProbeAsync(output, cancellationToken);
        return probe.Map(info =>
        {
            FinishedEpisode finished = episodes.Finish(status.Episode, output);
            reporter.Success($"Built {output} ({info.Duration})");
            finished.Opened.Tap(opened => reporter.Success($"Opened \"{opened.Name}\" for new clips"));
            return new BuiltEpisode(new BuildEpisodeResult(finished.Built, output, info.Duration, finished.Opened), status.Clips, converted);
        });
    }

    /// <summary>Step 4 (with --upload): upload the episode and its clips, then delete local copies with --clean-local.</summary>
    private async Task<Result<BuildEpisodeResult, Error>> UploadAsync(
        BuiltEpisode built, BuildEpisodeRequest request, CancellationToken cancellationToken)
    {
        if (!request.Upload)
        {
            return built.Result;
        }

        Result<IReadOnlyList<string>, Error> upload =
            await uploader.UploadEpisodeAsync(built.Result.Episode, built.Clips, request.Storage, cancellationToken);
        return upload
            .TapIf(request.CleanLocal, () => CleanLocal(built))
            .Map(_ => built.Result);
    }

    private void CleanLocal(BuiltEpisode built)
    {
        foreach (string path in built.Clips.Select(clip => clip.FilePath).Concat(built.Converted))
        {
            File.Delete(path);
        }

        reporter.Success($"Deleted {built.Clips.Count} local clip files (they are uploaded)");
    }

    private sealed record BuiltEpisode(BuildEpisodeResult Result, IReadOnlyList<Clip> Clips, IReadOnlyList<string> Converted);
}
