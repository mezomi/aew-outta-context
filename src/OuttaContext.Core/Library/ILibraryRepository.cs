namespace OuttaContext.Core.Library;

/// <summary>Clips, episodes, uploads and scraped links (library.db).</summary>
public interface ILibraryRepository
{
    // Clips
    Clip AddClip(NewClip clip);

    Clip GetClip(long id);

    Clip? FindClipByTitle(string title);

    IReadOnlyList<Clip> ListClips();

    IReadOnlyList<Clip> EpisodeClips(long episodeId);

    void AttachClip(long clipId, long episodeId);

    void DetachClip(long clipId);

    void Reorder(long episodeId, IReadOnlyList<long> clipIds);

    // Episodes
    Episode CreateEpisode(string name, double targetLength);

    Episode GetEpisode(long id);

    Episode? FindEpisode(string name);

    IReadOnlyList<Episode> ListEpisodes();

    Episode? LatestOpenEpisode();

    Episode MarkBuilt(long episodeId, string outputPath);

    // Uploads
    void RecordUpload(string kind, string reference, string localPath, string storage, string remotePath);

    UploadRecord? FindUpload(string localPath, string storage, string remotePath);

    // Links found by `outta scrape` (the page is used as referer later)
    void RememberLinks(IEnumerable<(string VideoUrl, string PageUrl, string Kind)> links);

    string? RefererFor(string videoUrl);
}

/// <summary>The details of a clip about to be saved.</summary>
public sealed record NewClip(
    string Title, string Source, string SourceUrl, double Start, double End, double Duration, string FilePath, string? PageUrl);
