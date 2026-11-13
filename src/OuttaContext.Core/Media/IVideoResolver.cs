using OuttaContext.Core.Common;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Media;

/// <summary>Turns a video page link into the real stream addresses (done by yt-dlp).</summary>
public interface IVideoResolver
{
    /// <returns>The video, or why the site refused (e.g. "yt-dlp.forbidden", "yt-dlp.network").</returns>
    Task<Result<ResolvedVideo, Error>> ResolveAsync(RequestVariant request, VideoQuality maxQuality, CancellationToken cancellationToken);
}
