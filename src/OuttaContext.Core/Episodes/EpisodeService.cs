using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Episodes;

/// <summary>
/// The rules for episodes: there is always one open "current" episode that new clips go into;
/// clips can be moved or removed; building marks it built and opens the next one.
/// </summary>
public sealed partial class EpisodeService(ILibraryRepository repository, double targetLength)
{
    public double TargetLength { get; set; } = targetLength;

    /// <summary>"Episode N+1" where N is the highest numbered episode so far.</summary>
    public string NextName()
    {
        int highest = repository.ListEpisodes()
            .Select(episode => NumberedEpisodeRegex().Match(episode.Name))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
        return string.Create(CultureInfo.InvariantCulture, $"Episode {highest + 1}");
    }

    /// <summary>The open episode new clips go into; "Episode 1" is created on first use.</summary>
    public Episode Current() => repository.LatestOpenEpisode() ?? repository.CreateEpisode(NextName(), TargetLength);

    public Episode Get(string? name) =>
        name is null
            ? Current()
            : repository.FindEpisode(name)
              ?? throw new NotFoundException($"There is no episode named \"{name}\".", "See episodes with `outta episode list`.");

    public Episode StartNew(string name)
    {
        string cleaned = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (cleaned.Length == 0)
        {
            throw new AlreadyExistsException("The episode name is empty.", "Give it a name, e.g. \"Episode 4\".");
        }

        return repository.CreateEpisode(cleaned, TargetLength);
    }

    public EpisodeStatus Status(string? name = null)
    {
        Episode episode = Get(name);
        return new EpisodeStatus(episode, repository.EpisodeClips(episode.Id));
    }

    public IReadOnlyList<EpisodeStatus> AllStatuses()
    {
        Current();
        return repository.ListEpisodes()
            .Select(episode => new EpisodeStatus(episode, repository.EpisodeClips(episode.Id)))
            .ToList();
    }

    public EpisodeStatus AddClip(Clip clip, string? episodeName = null)
    {
        Episode episode = Get(episodeName);
        if (episode.IsBuilt)
        {
            throw new EpisodeBuiltException($"\"{episode.Name}\" is already built.");
        }

        repository.AttachClip(clip.Id, episode.Id);
        return Status(episode.Name);
    }

    public EpisodeStatus Move(long clipId, int position)
    {
        (Clip clip, Episode episode) = ClipInEpisode(clipId);
        var ids = repository.EpisodeClips(episode.Id).Select(c => c.Id).ToList();
        if (position < 1 || position > ids.Count)
        {
            throw new OuttaException(
                $"Position {position} is outside 1-{ids.Count} for \"{episode.Name}\".",
                "Positions start at 1. See the order with `outta episode status`.");
        }

        ids.Remove(clip.Id);
        ids.Insert(position - 1, clip.Id);
        repository.Reorder(episode.Id, ids);
        return Status(episode.Name);
    }

    public (Clip Clip, EpisodeStatus Status) Remove(long clipId)
    {
        (Clip clip, Episode episode) = ClipInEpisode(clipId);
        repository.DetachClip(clip.Id);
        return (clip, Status(episode.Name));
    }

    /// <summary>Marks the episode built and opens the next one if no other episode is open.</summary>
    public (Episode Built, Episode? Opened) Finish(Episode episode, string outputPath)
    {
        Episode built = repository.MarkBuilt(episode.Id, outputPath);
        Episode? opened = repository.LatestOpenEpisode() is null
            ? repository.CreateEpisode(NextName(), TargetLength)
            : null;
        return (built, opened);
    }

    private (Clip Clip, Episode Episode) ClipInEpisode(long clipId)
    {
        Clip clip = repository.GetClip(clipId);
        return clip.EpisodeId is { } episodeId
            ? (clip, repository.GetEpisode(episodeId))
            : throw new NotFoundException(
                $"Clip {clipId} (\"{clip.Title}\") is not in an episode.", "See clip IDs with `outta episode status`.");
    }

    [GeneratedRegex(@"^Episode (\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NumberedEpisodeRegex();
}
