using OuttaContext.Core.Clips;
using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Library;

/// <summary>Clips, episodes, uploads and scraped links (library.db).</summary>
/// <remarks>"Find" methods return <see cref="Maybe{T}"/>: not finding something is normal.</remarks>
public interface ILibraryRepository
{
    // Clips
    Clip AddClip(NewClip clip);

    /// <summary>Overwrites clip <paramref name="id"/> with new details; its ID, episode and position stay.</summary>
    Clip ReplaceClip(long id, NewClip clip);

    Maybe<Clip> FindClip(long id);

    Maybe<Clip> FindClipByTitle(ClipTitle title);

    IReadOnlyList<Clip> ListClips();

    IReadOnlyList<Clip> EpisodeClips(long episodeId);

    void AttachClip(long clipId, long episodeId);

    void DetachClip(long clipId);

    void Reorder(long episodeId, IReadOnlyList<long> clipIds);

    // Episodes

    /// <summary>Creates an open episode. The caller checks the name is free first.</summary>
    Episode CreateEpisode(EpisodeName name, VideoTime targetLength);

    Maybe<Episode> FindEpisode(long id);

    Maybe<Episode> FindEpisode(EpisodeName name);

    IReadOnlyList<Episode> ListEpisodes();

    Maybe<Episode> LatestOpenEpisode();

    Episode MarkBuilt(long episodeId, string outputPath);

    // Uploads
    void RecordUpload(UploadKind kind, string reference, string localPath, string storage, string remotePath);

    Maybe<UploadRecord> FindUpload(string localPath, string storage, string remotePath);

    // Links found by `outta scrape` (the page is used as referer later)
    void RememberLinks(IEnumerable<ScrapedLink> links);

    Maybe<string> RefererFor(string videoUrl);
}
