namespace OuttaContext.Core.Library;

/// <summary>A saved clip. <see cref="EpisodeId"/>/<see cref="Position"/> are set when it is in an episode.</summary>
public sealed record Clip(
    long Id,
    string Title,
    string Source,
    string SourceUrl,
    double Start,
    double End,
    double Duration,
    string FilePath,
    string? PageUrl,
    string CreatedAt,
    long? EpisodeId = null,
    int? Position = null);

/// <summary>An episode: an ordered list of clips with a target length.</summary>
public sealed record Episode(
    long Id,
    string Name,
    double TargetLength,
    string State,
    string? OutputPath,
    string CreatedAt,
    string? BuiltAt)
{
    public const string Open = "open";
    public const string Built = "built";

    public bool IsBuilt => State == Built;
}

/// <summary>An episode with its clips, plus the totals shown by `outta episode status`.</summary>
public sealed record EpisodeStatus(Episode Episode, IReadOnlyList<Clip> Clips)
{
    public double Total => Clips.Sum(clip => clip.Duration);

    public double Remaining => Math.Max(0, Episode.TargetLength - Total);

    public bool IsReady => Total >= Episode.TargetLength;
}

/// <summary>A file that was uploaded, so the same unchanged file is not uploaded twice.</summary>
public sealed record UploadRecord(
    long Id, string Kind, string Reference, string LocalPath, string Storage, string RemotePath, long Size, string UploadedAt);
