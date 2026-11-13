using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Settings;

namespace OuttaContext.Core.Episodes;

/// <summary>
/// The rules for episodes: there is always one open "current" episode that new clips go into;
/// clips can be moved or removed; building marks it built and opens the next one.
/// </summary>
public sealed partial class EpisodeService(ILibraryRepository repository, OuttaSettings settings)
{
    /// <summary>"Episode N+1" where N is the highest numbered episode so far.</summary>
    public EpisodeName NextName()
    {
        int highest = repository.ListEpisodes()
            .Select(episode => NumberedEpisodeRegex().Match(episode.Name.Value))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
        return EpisodeName.FromTrusted(string.Create(CultureInfo.InvariantCulture, $"Episode {highest + 1}"));
    }

    /// <summary>The open episode new clips go into; "Episode 1" is created on first use.</summary>
    public Episode Current() =>
        repository.LatestOpenEpisode().GetValueOrDefault() ?? repository.CreateEpisode(NextName(), settings.EpisodeLength);

    /// <summary>The episode with this name, or the current one when no name is given.</summary>
    public Result<Episode, Error> Find(string? name) =>
        name is null
            ? Current()
            : EpisodeName.Create(name).Bind(episodeName => repository.FindEpisode(episodeName).ToResult(EpisodeErrors.NotFound(episodeName)));

    /// <summary>The episode a new clip would go into; fails if it is already built.</summary>
    public Result<Episode, Error> FindOpen(string? name) =>
        Find(name).Ensure(episode => !episode.IsBuilt, episode => EpisodeErrors.AlreadyBuilt(episode));

    public Result<Episode, Error> StartNew(string name) =>
        EpisodeName.Create(name)
            .Ensure(episodeName => repository.FindEpisode(episodeName).HasNoValue, episodeName => EpisodeErrors.NameTaken(episodeName))
            .Map(episodeName => repository.CreateEpisode(episodeName, settings.EpisodeLength));

    public Result<EpisodeStatus, Error> Status(string? name = null) => Find(name).Map(StatusOf);

    public IReadOnlyList<EpisodeStatus> AllStatuses()
    {
        Current();
        return repository.ListEpisodes().Select(StatusOf).ToList();
    }

    public Result<EpisodeStatus, Error> AddClip(Clip clip, string? episodeName = null) =>
        FindOpen(episodeName)
            .Tap(episode => repository.AttachClip(clip.Id, episode.Id))
            .Map(StatusOf);

    public Result<EpisodeStatus, Error> Move(long clipId, int position) =>
        ClipInEpisode(clipId).Bind(found =>
        {
            var ids = repository.EpisodeClips(found.Episode.Id).Select(clip => clip.Id).ToList();
            if (position < 1 || position > ids.Count)
            {
                return Result.Failure<EpisodeStatus, Error>(EpisodeErrors.PositionOutOfRange(position, ids.Count, found.Episode));
            }

            ids.Remove(found.Clip.Id);
            ids.Insert(position - 1, found.Clip.Id);
            repository.Reorder(found.Episode.Id, ids);
            return StatusOf(found.Episode);
        });

    public Result<RemovedClip, Error> Remove(long clipId) =>
        ClipInEpisode(clipId)
            .Tap(found => repository.DetachClip(found.Clip.Id))
            .Map(found => new RemovedClip(found.Clip, StatusOf(found.Episode)));

    /// <summary>Marks the episode built and opens the next one if no other episode is open.</summary>
    public FinishedEpisode Finish(Episode episode, string outputPath)
    {
        Episode built = repository.MarkBuilt(episode.Id, outputPath);
        Maybe<Episode> opened = repository.LatestOpenEpisode().HasValue
            ? Maybe<Episode>.None
            : repository.CreateEpisode(NextName(), settings.EpisodeLength);
        return new FinishedEpisode(built, opened);
    }

    private EpisodeStatus StatusOf(Episode episode) => new(episode, repository.EpisodeClips(episode.Id));

    private Result<(Clip Clip, Episode Episode), Error> ClipInEpisode(long clipId) =>
        repository.FindClip(clipId)
            .ToResult(EpisodeErrors.ClipNotFound(clipId))
            .Bind(clip => clip.EpisodeId
                .Bind(repository.FindEpisode)
                .Map(episode => (clip, episode))
                .ToResult(EpisodeErrors.ClipNotInEpisode(clip)));

    [GeneratedRegex(@"^Episode (\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NumberedEpisodeRegex();
}
