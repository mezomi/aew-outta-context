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
/// With --replace [ID] it overwrites an existing clip instead, which keeps its ID and episode place.
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
            .Bind(SaveClip)
            .Bind(saved => AddToEpisode(saved, request))
            .Bind(result => UploadAsync(result, request, cancellationToken));

    /// <summary>Step 1: everything that can be checked without the network.</summary>
    private Result<ClipPlan, Error> Check(MakeClipRequest request) =>
        ClipTitle.Create(request.Title)
            .Bind(title => ChooseDestination(title, request.Replace).Map(target => (title, target)))
            .Bind(named => ClipTimeSpan.Parse(request.Range).Map(span => (named.title, named.target, span)))
            .Bind(parts => EnsureUnderClipCap(parts.span).Map(() => parts))
            .Bind(parts => sources.Detect(request.Url).Map(link => (parts.title, parts.target, parts.span, link)))
            .Bind(parts => EnsureEpisodeIsOpen(request).Map(() => parts))
            .Map(parts => new ClipPlan(
                parts.title,
                parts.target.OutputPath,
                parts.span,
                parts.link,
                request.Referer is null ? repository.RefererFor(parts.link.CanonicalUrl) : request.Referer)
            {
                Replacing = parts.target.Replacing,
            });

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

        // A replacement is downloaded next to the old clip first, so a failed download changes nothing.
        string downloadPath = plan.Replacing.HasValue ? NewCopyPath(plan.OutputPath) : plan.OutputPath;
        Result<DownloadedClip, Error> download =
            await downloader.DownloadAsync(plan.Video.Value, downloadPath, plan.Span, cancellationToken);
        if (download.IsFailure)
        {
            return download.Error;
        }

        VideoTime length = await MeasureAsync(download.Value.Path, plan.Span.Length, cancellationToken);
        return (plan, download.Value, length);
    }

    /// <summary>Step 4: save it in the library (or, with --replace, put it in the old clip's place).</summary>
    private Result<SavedClip, Error> SaveClip((ClipPlan Plan, DownloadedClip Saved, VideoTime Length) downloaded)
    {
        (ClipPlan plan, DownloadedClip saved, VideoTime length) = downloaded;
        var details = new NewClip(
            plan.Title, plan.Link.Source.Name, plan.Link.CanonicalUrl, plan.Span, length, plan.OutputPath, plan.Referer);

        if (!plan.Replacing.TryGetValue(out Clip? old))
        {
            Clip clip = repository.AddClip(details);
            reporter.Success($"Saved {saved.Path} (clip {clip.Id}, {length.ToShortLength()}, {saved.Size})");
            return new SavedClip(clip, Replaced: false);
        }

        return MoveIntoPlace(saved.Path, old.FilePath, plan.OutputPath)
            .Map(() => repository.ReplaceClip(old.Id, details))
            .Tap(clip => reporter.Success(
                $"Replaced clip {clip.Id} (\"{old.Title}\", {old.Span}) with {clip.FilePath} ({plan.Span}, {length.ToShortLength()}, {saved.Size})"))
            .Map(clip => new SavedClip(clip, Replaced: true));
    }

    /// <summary>Step 5: add it to the episode (unless --no-episode). A replaced clip stays where it was.</summary>
    private Result<MakeClipResult, Error> AddToEpisode(SavedClip saved, MakeClipRequest request)
    {
        Clip clip = saved.Clip;
        if (saved.Replaced)
        {
            Maybe<EpisodeStatus> status = episodes.StatusOfClip(clip);
            status.Tap(found => reporter.Success($"Still at position {clip.Position.GetValueOrDefault()} of " + found.Describe()));
            status.Where(found => found.Episode.IsBuilt).Tap(found => reporter.Warn(
                $"\"{found.Episode.Name}\" was already built. Rebuild it with `outta episode build \"{found.Episode.Name}\"`."));
            return new MakeClipResult(clip, status);
        }

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

    /// <summary>
    /// Where the clip goes, and which clip it replaces (with --replace). Without --replace a taken
    /// title is refused. A file on disk that isn't the replaced clip's own is never overwritten.
    /// </summary>
    private Result<Destination, Error> ChooseDestination(ClipTitle title, ClipReplacement replace)
    {
        Maybe<Clip> sameTitle = repository.FindClipByTitle(title);
        Result<Maybe<Clip>, Error> replacing = (replace.IsRequested, replace.ClipId.HasValue) switch
        {
            (false, _) => sameTitle.TryGetValue(out Clip? taken)
                ? ClipErrors.DuplicateTitle(taken.Title, taken.Id)
                : Maybe<Clip>.None,
            (true, false) => sameTitle.ToResult(ClipErrors.NothingToReplace(title)).Map(Maybe.From),
            (true, true) => repository.FindClip(replace.ClipId.Value)
                .ToResult(ClipErrors.NotFound(replace.ClipId.Value))
                .Ensure(
                    target => !sameTitle.TryGetValue(out Clip? taken) || taken.Id == target.Id,
                    target => ClipErrors.TitleTakenByAnother(title, sameTitle.Value.Id, target.Id))
                .Map(Maybe.From),
        };

        string outputPath = Path.Combine(settings.ClipFolder, title.FileName + ".mp4");
        return replacing
            .Ensure(
                target => !File.Exists(outputPath) || target.Map(old => SamePath(old.FilePath, outputPath)).GetValueOrDefault(false),
                ClipErrors.FileExists(outputPath))
            .Map((Maybe<Clip> target) => new Destination(outputPath, target));
    }

    /// <summary>Swaps the downloaded replacement in for the old clip's file.</summary>
    private static UnitResult<Error> MoveIntoPlace(string newCopy, string oldFile, string outputPath)
    {
        try
        {
            if (!SamePath(oldFile, outputPath))
            {
                File.Delete(oldFile);
            }

            File.Move(newCopy, outputPath, overwrite: true);
            return UnitResult.Success<Error>();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            File.Delete(newCopy);
            return ClipErrors.CannotReplaceFile(oldFile, exception.Message);
        }
    }

    private static string NewCopyPath(string outputPath) =>
        Path.Combine(Path.GetDirectoryName(outputPath)!, Path.GetFileNameWithoutExtension(outputPath) + ".new.mp4");

    private static bool SamePath(string first, string second) =>
        string.Equals(
            Path.GetFullPath(first),
            Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private UnitResult<Error> EnsureUnderClipCap(ClipTimeSpan span) =>
        settings.MaxClipLength.TryGetValue(out VideoTime? cap) && span.Length.IsLongerThan(cap)
            ? ClipErrors.TooLong(span, cap)
            : UnitResult.Success<Error>();

    /// <summary>The episode a new clip goes into must be open. A replaced clip stays in its own episode.</summary>
    private UnitResult<Error> EnsureEpisodeIsOpen(MakeClipRequest request) =>
        request.NoEpisode || request.Replace.IsRequested
            ? UnitResult.Success<Error>()
            : episodes.FindOpen(request.Episode).DropValue();

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
        /// <summary>The clip being replaced (with --replace).</summary>
        public Maybe<Clip> Replacing { get; init; }

        /// <summary>Set once the site answered (step 2).</summary>
        public Maybe<ResolvedVideo> Video { get; init; }
    }

    /// <summary>Where the clip file goes, and the clip it replaces, if any.</summary>
    private sealed record Destination(string OutputPath, Maybe<Clip> Replacing);

    /// <summary>A clip in the library, and whether it replaced an older one.</summary>
    private sealed record SavedClip(Clip Clip, bool Replaced);
}
