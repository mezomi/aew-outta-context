using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>What yt-dlp tells us about a video.</summary>
/// <param name="Duration">The video's length; some sites don't say.</param>
public sealed record VideoInfo(string VideoId, string Title, Maybe<VideoTime> Duration, string WebpageUrl, string Extractor);
