using OuttaContext.Core.Episodes;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Time;
using OuttaContext.Core.Uploads;

namespace OuttaContext.Core.Clips;

/// <summary>
/// `outta clip`: check the request → find the video → check the range → download the clip →
/// save it → add it to the episode → upload it (optional).
/// </summary>
/// <remarks>
/// Cheap checks (title, range, clip cap, episode) run first, so nothing is downloaded for a
/// request that can't succeed.
/// </remarks>
public sealed class MakeClipHandler(
    SourceRegistry sources,
    IVideoResolver resolver,
    IClipDownloader downloader,
    IMediaProcessor media,
    ILibraryRepository repository,
    EpisodeService episodes,
    OuttaSettings settings,
    IReporter reporter,
    IClipUploader? uploader = null)
{
    public async Task<MakeClipResult> HandleAsync(MakeClipRequest request, CancellationToken cancellationToken)
    {
        // 1. Checks that need no network.
        string title = ClipTitle.Clean(request.Title);
        string outputPath = Path.Combine(settings.ClipFolder, ClipTitle.ToFileName(title) + ".mp4");
        EnsureTitleIsFree(title, outputPath);
        TimeRange range = TimeRange.Parse(request.Range);
        EnsureUnderClipCap(range);
        VideoSource source = sources.Detect(request.Url);
        string canonicalUrl = source.CanonicalUrl(request.Url);
        EnsureEpisodeIsOpen(request);
        string? referer = request.Referer ?? repository.RefererFor(canonicalUrl);

        // 2. Find the video and check the range against its real length.
        reporter.Step($"Detected source: {source.Label}");
        if (referer is not null)
        {
            reporter.Detail($"Referer: {referer}");
        }

        reporter.Step("Reading video info...");
        ResolvedVideo video = await ResolveAsync(source.RequestVariants(request.Url, referer), cancellationToken);
        string length = video.Info.Duration is { } duration ? $" ({TimeText.Format(duration)})" : string.Empty;
        reporter.Step($"Video: \"{video.Info.Title}\"{length}");
        if (video.Info.Duration is null)
        {
            reporter.Warn("The site did not report the video's length; the range can't be checked first.");
        }

        range.EnsureWithin(video.Info.Duration);

        // 3. Download just that range.
        reporter.Step($"Clipping {range} ({TimeText.FormatLength(range.Length)}, best up to {settings.MaxQuality}p)");
        ToolResult<DownloadedClip> download =
            await downloader.DownloadAsync(video, outputPath, range, reporter, cancellationToken);
        DownloadedClip saved = download.ValueOrThrow();

        // 4. Save, add to the episode, upload.
        double clipLength = await MeasureAsync(saved.Path, range.Length, cancellationToken);
        Clip clip = repository.AddClip(new NewClip(
            title, source.Name, canonicalUrl, range.Start, range.End, clipLength, saved.Path, referer));
        reporter.Success($"Saved {saved.Path} (clip {clip.Id}, {TimeText.FormatLength(clipLength)})");

        EpisodeStatus? status = null;
        if (!request.NoEpisode)
        {
            status = episodes.AddClip(clip, request.Episode);
            reporter.Success(DescribeStatus(status));
            if (status.IsReady)
            {
                reporter.Warn($"\"{status.Episode.Name}\" has reached its target length. Build it with `outta episode build`.");
            }
        }

        if (request.Upload)
        {
            if (uploader is null)
            {
                throw new OuttaException("Uploading is not available.");
            }

            await uploader.UploadClipAsync(clip, request.Storage, cancellationToken);
        }

        return new MakeClipResult(clip, status);
    }

    public static string DescribeStatus(EpisodeStatus status)
    {
        string line = $"Added to \"{status.Episode.Name}\": {TimeText.Format(status.Total)} of {TimeText.Format(status.Episode.TargetLength)}";
        return status.IsReady ? line + " (ready to build)" : line + $" ({TimeText.Format(status.Remaining)} left)";
    }

    /// <summary>Tries each way of asking for the video until one works.</summary>
    private async Task<ResolvedVideo> ResolveAsync(IReadOnlyList<RequestVariant> variants, CancellationToken cancellationToken)
    {
        ToolError? lastError = null;
        for (int index = 0; index < variants.Count; index++)
        {
            RequestVariant variant = variants[index];
            if (index > 0)
            {
                reporter.Step($"Retrying as {variant.Label}...");
            }

            reporter.Detail($"Request: {variant.Label}");
            ToolResult<ResolvedVideo> result =
                await resolver.ResolveAsync(variant, settings.MaxQuality, reporter.Detail, cancellationToken);
            if (result.Succeeded)
            {
                return result.Value;
            }

            lastError = result.Error;
            reporter.Detail($"{variant.Label} failed: {result.Error.Message}");
        }

        throw (lastError ?? new ToolError("yt-dlp", "No way to request this video.", string.Empty)).ToException();
    }

    private void EnsureTitleIsFree(string title, string outputPath)
    {
        if (repository.FindClipByTitle(title) is { } existing)
        {
            throw new DuplicateTitleException($"A clip titled \"{existing.Title}\" already exists (ID {existing.Id}).");
        }

        if (File.Exists(outputPath))
        {
            throw new DuplicateTitleException(
                $"{outputPath} already exists.", "Pick a different --title, or move that file away.");
        }
    }

    private void EnsureUnderClipCap(TimeRange range)
    {
        const double Tolerance = 1e-6;
        if (settings.MaxClipLength is { } cap && range.Length > cap + Tolerance)
        {
            throw new ClipTooLongException(
                $"{range} is {TimeText.FormatLength(range.Length)} long, over the {TimeText.FormatLength(cap)} clip cap.");
        }
    }

    private void EnsureEpisodeIsOpen(MakeClipRequest request)
    {
        if (!request.NoEpisode && episodes.Get(request.Episode) is { IsBuilt: true } built)
        {
            throw new EpisodeBuiltException($"\"{built.Name}\" is already built.");
        }
    }

    /// <summary>The clip's real length (ffprobe), or the requested length if that isn't possible.</summary>
    private async Task<double> MeasureAsync(string path, double fallback, CancellationToken cancellationToken)
    {
        if (!media.IsAvailable)
        {
            reporter.Detail("ffprobe not found; using the requested length");
            return fallback;
        }

        ToolResult<ProbeResult> probe = await media.ProbeAsync(path, cancellationToken);
        return probe.Succeeded && probe.Value.Duration > 0 ? probe.Value.Duration : fallback;
    }
}
