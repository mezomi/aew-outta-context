using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Time;

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
        if (FindClipByTitle(clip.Title).HasValue)
        {
            throw new InvalidOperationException("UNIQUE constraint failed: clips.title");
        }

        var saved = new Clip(_clips.Count + 1, clip.Title, clip.Source, clip.SourceUrl, clip.Span, clip.Duration, clip.FilePath, clip.PageUrl, "now");
        _clips.Add(saved);
        return saved;
    }

    public Clip ReplaceClip(long id, NewClip clip)
    {
        Clip replaced = GetClip(id) with
        {
            Title = clip.Title,
            Source = clip.Source,
            SourceUrl = clip.SourceUrl,
            Span = clip.Span,
            Duration = clip.Duration,
            FilePath = clip.FilePath,
            PageUrl = clip.PageUrl,
        };
        Replace(replaced);
        return replaced;
    }

    public Maybe<Clip> FindClip(long id) => _clips.TryFirst(c => c.Id == id);

    public Maybe<Clip> FindClipByTitle(ClipTitle title) =>
        _clips.TryFirst(c => string.Equals(c.Title.Value, title.Value, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Clip> ListClips() => [.. _clips];

    public IReadOnlyList<Clip> EpisodeClips(long episodeId) =>
        _clips.Where(c => c.EpisodeId == episodeId).OrderBy(c => c.Position.GetValueOrDefault()).ThenBy(c => c.Id).ToList();

    public void AttachClip(long clipId, long episodeId) =>
        Replace(GetClip(clipId) with { EpisodeId = episodeId, Position = EpisodeClips(episodeId).Count + 1 });

    public void DetachClip(long clipId)
    {
        Clip clip = GetClip(clipId);
        Replace(clip with { EpisodeId = Maybe<long>.None, Position = Maybe<int>.None });
        clip.EpisodeId.Tap(episodeId => Reorder(episodeId, EpisodeClips(episodeId).Select(c => c.Id).ToList()));
    }

    public void Reorder(long episodeId, IReadOnlyList<long> clipIds)
    {
        for (int index = 0; index < clipIds.Count; index++)
        {
            Replace(GetClip(clipIds[index]) with { Position = index + 1 });
        }
    }

    public Episode CreateEpisode(EpisodeName name, VideoTime targetLength)
    {
        if (FindEpisode(name).HasValue)
        {
            throw new InvalidOperationException("UNIQUE constraint failed: episodes.name");
        }

        var episode = new Episode(_episodes.Count + 1, name, targetLength, EpisodeState.Open, Maybe<string>.None, "now", Maybe<string>.None);
        _episodes.Add(episode);
        return episode;
    }

    public Maybe<Episode> FindEpisode(long id) => _episodes.TryFirst(e => e.Id == id);

    public Maybe<Episode> FindEpisode(EpisodeName name) =>
        _episodes.TryFirst(e => string.Equals(e.Name.Value, name.Value, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Episode> ListEpisodes() => [.. _episodes];

    public Maybe<Episode> LatestOpenEpisode() => _episodes.TryLast(e => e.State == EpisodeState.Open);

    public Episode MarkBuilt(long episodeId, string outputPath)
    {
        int index = _episodes.FindIndex(e => e.Id == episodeId);
        _episodes[index] = _episodes[index] with { State = EpisodeState.Built, OutputPath = outputPath, BuiltAt = "now" };
        return _episodes[index];
    }

    public void RecordUpload(UploadKind kind, string reference, string localPath, string storage, string remotePath) =>
        _uploads.Add(new UploadRecord(
            _uploads.Count + 1, kind, reference, Path.GetFullPath(localPath), storage, remotePath, ByteSize.OfFile(localPath), "earlier"));

    public Maybe<UploadRecord> FindUpload(string localPath, string storage, string remotePath) =>
        _uploads.TryLast(u => u.LocalPath == Path.GetFullPath(localPath) && u.Storage == storage && u.RemotePath == remotePath
            && u.Size == ByteSize.OfFile(localPath));

    public void RememberLinks(IEnumerable<ScrapedLink> links)
    {
        foreach (ScrapedLink link in links)
        {
            _referers[link.VideoUrl] = link.PageUrl;
        }
    }

    public Maybe<string> RefererFor(string videoUrl) => _referers.TryGetValue(videoUrl, out string? page) ? page : Maybe<string>.None;

    private Clip GetClip(long id) => FindClip(id).GetValueOrThrow($"No clip {id}");

    private void Replace(Clip clip) => _clips[_clips.FindIndex(c => c.Id == clip.Id)] = clip;
}
