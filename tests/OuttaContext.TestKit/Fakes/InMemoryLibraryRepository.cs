using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;

namespace OuttaContext.TestKit.Fakes;

/// <summary>A library kept in memory, with the same rules as the SQLite one (unique titles and names, ignoring case).</summary>
public sealed class InMemoryLibraryRepository : ILibraryRepository
{
    private readonly List<Clip> _clips = [];
    private readonly List<Episode> _episodes = [];
    private readonly List<UploadRecord> _uploads = [];
    private readonly Dictionary<string, string> _referers = new(StringComparer.Ordinal);

    public Clip AddClip(NewClip clip)
    {
        if (FindClipByTitle(clip.Title) is not null)
        {
            throw new InvalidOperationException("UNIQUE constraint failed: clips.title");
        }

        var saved = new Clip(_clips.Count + 1, clip.Title, clip.Source, clip.SourceUrl, clip.Start, clip.End, clip.Duration, clip.FilePath, clip.PageUrl, "now");
        _clips.Add(saved);
        return saved;
    }

    public Clip GetClip(long id) =>
        _clips.SingleOrDefault(c => c.Id == id) ?? throw new NotFoundException($"There is no clip with ID {id}.", "See clip IDs.");

    public Clip? FindClipByTitle(string title) => _clips.SingleOrDefault(c => string.Equals(c.Title, title, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Clip> ListClips() => [.. _clips];

    public IReadOnlyList<Clip> EpisodeClips(long episodeId) =>
        _clips.Where(c => c.EpisodeId == episodeId).OrderBy(c => c.Position).ThenBy(c => c.Id).ToList();

    public void AttachClip(long clipId, long episodeId) =>
        Replace(GetClip(clipId) with { EpisodeId = episodeId, Position = EpisodeClips(episodeId).Count + 1 });

    public void DetachClip(long clipId)
    {
        Clip clip = GetClip(clipId);
        Replace(clip with { EpisodeId = null, Position = null });
        if (clip.EpisodeId is { } episodeId)
        {
            Reorder(episodeId, EpisodeClips(episodeId).Select(c => c.Id).ToList());
        }
    }

    public void Reorder(long episodeId, IReadOnlyList<long> clipIds)
    {
        for (int index = 0; index < clipIds.Count; index++)
        {
            Replace(GetClip(clipIds[index]) with { Position = index + 1 });
        }
    }

    public Episode CreateEpisode(string name, double targetLength)
    {
        if (FindEpisode(name) is not null)
        {
            throw new AlreadyExistsException($"An episode named \"{name}\" already exists.", "Pick another name.");
        }

        var episode = new Episode(_episodes.Count + 1, name, targetLength, Episode.Open, null, "now", null);
        _episodes.Add(episode);
        return episode;
    }

    public Episode GetEpisode(long id) =>
        _episodes.SingleOrDefault(e => e.Id == id) ?? throw new NotFoundException($"There is no episode with ID {id}.", "See episodes.");

    public Episode? FindEpisode(string name) =>
        _episodes.SingleOrDefault(e => string.Equals(e.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Episode> ListEpisodes() => [.. _episodes];

    public Episode? LatestOpenEpisode() => _episodes.LastOrDefault(e => e.State == Episode.Open);

    public Episode MarkBuilt(long episodeId, string outputPath)
    {
        int index = _episodes.FindIndex(e => e.Id == episodeId);
        _episodes[index] = _episodes[index] with { State = Episode.Built, OutputPath = outputPath, BuiltAt = "now" };
        return _episodes[index];
    }

    public void RecordUpload(string kind, string reference, string localPath, string storage, string remotePath) =>
        _uploads.Add(new UploadRecord(_uploads.Count + 1, kind, reference, Path.GetFullPath(localPath), storage, remotePath, new FileInfo(localPath).Length, "earlier"));

    public UploadRecord? FindUpload(string localPath, string storage, string remotePath) =>
        _uploads.LastOrDefault(u => u.LocalPath == Path.GetFullPath(localPath) && u.Storage == storage && u.RemotePath == remotePath
            && u.Size == new FileInfo(localPath).Length);

    public void RememberLinks(IEnumerable<(string VideoUrl, string PageUrl, string Kind)> links)
    {
        foreach ((string videoUrl, string pageUrl, _) in links)
        {
            _referers[videoUrl] = pageUrl;
        }
    }

    public string? RefererFor(string videoUrl) => _referers.GetValueOrDefault(videoUrl);

    private void Replace(Clip clip) => _clips[_clips.FindIndex(c => c.Id == clip.Id)] = clip;
}
