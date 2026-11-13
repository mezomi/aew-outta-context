using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Time;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Writes a tiny file instead of downloading. Set <see cref="Failure"/> to make it fail.</summary>
public sealed class FakeClipDownloader : IClipDownloader
{
    public ToolError? Failure { get; set; }

    public List<(ResolvedVideo Video, string OutputPath, TimeRange Range)> Downloads { get; } = [];

    public Task<ToolResult<DownloadedClip>> DownloadAsync(
        ResolvedVideo video, string outputPath, TimeRange range, IReporter reporter, CancellationToken cancellationToken)
    {
        Downloads.Add((video, outputPath, range));
        if (Failure is { } failure)
        {
            return Task.FromResult(ToolResult<DownloadedClip>.Failure(failure));
        }

        using (IProgressTask progress = reporter.StartProgress("cutting", ProgressUnit.Seconds, range.Length))
        {
            progress.Report(range.Length, range.Length);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, new byte[100]);
        return Task.FromResult(ToolResult<DownloadedClip>.Success(new DownloadedClip(outputPath, 100)));
    }
}
