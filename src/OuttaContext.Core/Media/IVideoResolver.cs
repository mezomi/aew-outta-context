using OuttaContext.Core.Results;
using OuttaContext.Core.Sources;

namespace OuttaContext.Core.Media;

/// <summary>Turns a video page link into the real stream addresses (done by yt-dlp).</summary>
public interface IVideoResolver
{
    Task<ToolResult<ResolvedVideo>> ResolveAsync(
        RequestVariant request, int maxQuality, Action<string> detail, CancellationToken cancellationToken);
}
