using OuttaContext.Core.Common;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>Downloads just the part of a resolved video in <c>span</c> and saves it as an MP4.</summary>
public interface IClipDownloader
{
    Task<Result<DownloadedClip, Error>> DownloadAsync(
        ResolvedVideo video, string outputPath, ClipTimeSpan span, CancellationToken cancellationToken);
}
