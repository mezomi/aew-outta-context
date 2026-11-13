using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>Downloads just the part of a resolved video in <c>range</c> and saves it as an MP4.</summary>
public interface IClipDownloader
{
    Task<ToolResult<DownloadedClip>> DownloadAsync(
        ResolvedVideo video, string outputPath, TimeRange range, IReporter reporter, CancellationToken cancellationToken);
}
