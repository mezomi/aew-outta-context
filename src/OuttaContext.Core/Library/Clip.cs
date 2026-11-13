using OuttaContext.Core.Clips;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Library;

/// <summary>A saved clip. <see cref="EpisodeId"/> and <see cref="Position"/> are set when it is in an episode.</summary>
/// <param name="Source">The site's short name, e.g. "dailymotion".</param>
/// <param name="Span">The part of the source video that was clipped.</param>
/// <param name="Duration">The clip file's real length (measured with ffprobe when possible).</param>
/// <param name="PageUrl">The page the video was found on, used as referer.</param>
public sealed record Clip(
    long Id,
    ClipTitle Title,
    string Source,
    string SourceUrl,
    ClipTimeSpan Span,
    VideoTime Duration,
    string FilePath,
    Maybe<string> PageUrl,
    string CreatedAt,
    Maybe<long> EpisodeId = default,
    Maybe<int> Position = default);
