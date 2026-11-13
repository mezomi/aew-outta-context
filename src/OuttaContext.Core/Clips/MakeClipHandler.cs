using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
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
/// Each step returns a <c>Result</c>; the first failure stops the chain and is returned
/// ("railway" style). Cheap checks (title, range, clip cap, episode) run first, so nothing is
/// downloaded for a request that can't succeed.
/// </remarks>
public sealed class MakeClipHandler(
    SourceRegistry sources,
    IVideoResolver resolver,
    IClipDownloader downloader,
    IMediaProcessor media,
    ILibraryRepository repository,
    EpisodeService episodes,
    OuttaSettings settings,
    IClipUploader uploader,
    IReporter reporter)
{
    public Task<Result<MakeClipResult, Error>> HandleAsync(MakeClipRequest request, CancellationToken cancellationToken) =>
        Check(request)
            .Bind(plan => ResolveAsync(plan, cancellationToken))
            .Bind(plan => DownloadAsync(plan, cancellationToken))
            .Map(SaveClip)
            .Bind(clip => AddToEpisode(clip, request))
            .Bind(result => UploadAsync(result, request, cancellationToken));

    /// <summary>Step 1: everything that can be checked without the network.</summary>
    private Result<ClipPlan, Error> Check(MakeClipRequest request) =>
        ClipTitle.Create(request.Title)
            .Bind(title => EnsureTitleIsFree(title).Map(path => (title, path)))
            .Bind(named => ClipTimeSpan.Parse(request.Range).Map(span => (named.title, named.path, span)))
            .Bind(parts => EnsureUnderClipCap(parts.span).Map(() => parts))
            .Bind(parts => sources.Detect(request.Url).Map(link => (parts.title, parts.path, parts.span, link)))
            .Bind(parts => EnsureEpisodeIsOpen(request).Map(() => parts))
            .Map(parts => new ClipPlan(
                parts.title,
                parts.path,
                parts.span,
                parts.link,
                request.Referer is null ? repository.RefererFor(parts.link.CanonicalUrl) : request.Referer));

    /// <summary>Step 2: ask the site about the video, trying each way of asking until one works.</summary>
    private async Task<Result<ClipPlan, Error>> ResolveAsync(ClipPlan plan, CancellationToken cancellationToken)
    {
        reporter.Step($"Detected source: {plan.Link.Source.Label}");
        plan.Referer.Tap(referer => reporter.Detail($"Referer: {referer}"));
        reporter.Step("Reading video info...");

        IReadOnlyList<RequestVariant> variants = plan.Link.RequestVariants(plan.Referer);
        Result<ResolvedVideo, Error> result = SourceErrors.NoRequestVariants(plan.Link);
        for (int index = 0; index < variants.Count; index++)
        {
            RequestVariant variant = variants[index];
            if (index > 0)
            {
                reporter.Step($"Retrying as {variant.Label}...");
            }

            reporter.Detail($"Request: {variant.Label}");
            result = await resolver.ResolveAsync(variant, settings.MaxQuality, cancellationToken);
            if (result.IsSuccess)
            {
                break;
            }

            reporter.Detail($"{variant.Label} failed: {result.Error.Message}");
        }

        return result
            .Tap(ReportVideo)
            .Bind(video => plan.Span.FitsIn(video.Info.Duration).Map(() => plan with { Video = video }));
    }

    /// <summary>Step 3: download just the range.</summary>
    private async Task<Result<(ClipPlan Plan, DownloadedClip Saved, VideoTime Length), Error>> DownloadAsync(
        ClipPlan plan, CancellationToken cancellationToken)
    {
        reporter.Step($"Clipping {plan.Span} ({plan.Span.Length.ToShortLength()}, best up to {settings.MaxQuality})");
        Result<DownloadedClip, Error> download =
            await downloader.DownloadAsync(plan.Video.Value, plan.OutputPath, plan.Span, cancellationToken);
        if (download.IsFailure)
        {
            return download.Error;
        }

        VideoTime length = await MeasureAsync(download.Value.Path, plan.Span.Length, cancellationToken);
        return (plan, download.Value, length);
    }

    /// <summary>Step 4: save it in the library.</summary>
    private Clip SaveClip((ClipPlan Plan, DownloadedClip Saved, VideoTime Length) downloaded)
    {
        (ClipPlan plan, DownloadedClip saved, VideoTime length) = downloaded;
        Clip clip = repository.AddClip(new NewClip(
            plan.Title, plan.Link.Source.Name, plan.Link.CanonicalUrl, plan.Span, length, saved.Path, plan.Referer));
        reporter.Success($"Saved {saved.Path} (clip {clip.Id}, {length.ToShortLength()}, {saved.Size})");
        return clip;
    }

    /// <summary>Step 5: add it to the episode (unless --no-episode).</summary>
    private Result<MakeClipResult, Error> AddToEpisode(Clip clip, MakeClipRequest request)
    {
        if (request.NoEpisode)
        {
            return new MakeClipResult(clip, Maybe<EpisodeStatus>.None);
        }

        return episodes.AddClip(clip, request.Episode)
            .Tap(status => reporter.Success("Added to " + status.Describe()))
            .TapIf(status => status.IsReady, status => reporter.Warn(
                $"\"{status.Episode.Name}\" has reached its target length. Build it with `outta episode build`."))
            .Map(status => new MakeClipResult(clip, status));
    }

    /// <summary>Step 6: upload it (with --upload).</summary>
    private async Task<Result<MakeClipResult, Error>> UploadAsync(
        MakeClipResult result, MakeClipRequest request, CancellationToken cancellationToken)
    {
        if (!request.Upload)
        {
            return result;
        }

        Result<string, Error> upload = await uploader.UploadClipAsync(result.Clip, request.Storage, cancellationToken);
        return upload.Map(_ => result);
    }

    private void ReportVideo(ResolvedVideo video)
    {
        string length = video.Info.Duration.Map(duration => $" ({duration})").GetValueOrDefault(string.Empty);
        reporter.Step($"Video: \"{video.Info.Title}\"{length}");
        if (video.Info.Duration.HasNoValue)
        {
            reporter.Warn("The site did not report the video's length; the range can't be checked first.");
        }
    }

    /// <returns>The output path for the title, or why the title can't be used.</returns>
    private Result<string, Error> EnsureTitleIsFree(ClipTitle title)
    {
        if (repository.FindClipByTitle(title).TryGetValue(out Clip? existing))
        {
            return ClipErrors.DuplicateTitle(existing.Title, existing.Id);
        }

        string outputPath = Path.Combine(settings.ClipFolder, title.FileName + ".mp4");
        return File.Exists(outputPath) ? ClipErrors.FileExists(outputPath) : outputPath;
    }

    private UnitResult<Error> EnsureUnderClipCap(ClipTimeSpan span) =>
        settings.MaxClipLength.TryGetValue(out VideoTime? cap) && span.Length.IsLongerThan(cap)
            ? ClipErrors.TooLong(span, cap)
            : UnitResult.Success<Error>();

    private UnitResult<Error> EnsureEpisodeIsOpen(MakeClipRequest request) =>
        request.NoEpisode ? UnitResult.Success<Error>() : episodes.FindOpen(request.Episode).DropValue();

    /// <summary>The clip's real length (ffprobe), or the requested length if that isn't possible.</summary>
    private async Task<VideoTime> MeasureAsync(string path, VideoTime fallback, CancellationToken cancellationToken)
    {
        if (!media.IsAvailable)
        {
            reporter.Detail("ffprobe not found; using the requested length");
            return fallback;
        }

        Result<ProbeResult, Error> probe = await media.ProbeAsync(path, cancellationToken);
        return probe.IsSuccess && probe.Value.Duration.Seconds > 0 ? probe.Value.Duration : fallback;
    }

    /// <summary>What has been worked out so far about the clip being made.</summary>
    private sealed record ClipPlan(ClipTitle Title, string OutputPath, ClipTimeSpan Span, VideoLink Link, Maybe<string> Referer)
    {
        /// <summary>Set once the site answered (step 2).</summary>
        public Maybe<ResolvedVideo> Video { get; init; }
    }
}
