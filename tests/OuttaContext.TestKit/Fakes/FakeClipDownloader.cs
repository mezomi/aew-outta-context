using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Time;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Writes a tiny file instead of downloading. Set <see cref="Failure"/> to make it fail.</summary>
public sealed class FakeClipDownloader(IReporter? reporter = null) : IClipDownloader
{
    /// <summary>Where the progress bar goes (set later when the reporter comes from the DI container).</summary>
    public IReporter? Reporter { get; set; } = reporter;

    public Maybe<Error> Failure { get; set; }

    public List<(ResolvedVideo Video, string OutputPath, ClipTimeSpan Span)> Downloads { get; } = [];

    public Task<Result<DownloadedClip, Error>> DownloadAsync(
        ResolvedVideo video, string outputPath, ClipTimeSpan span, CancellationToken cancellationToken)
    {
        Downloads.Add((video, outputPath, span));
        if (Failure.TryGetValue(out Error? failure))
        {
            return Task.FromResult(Result.Failure<DownloadedClip, Error>(failure));
        }

        if (Reporter is not null)
        {
            using IProgressTask progress = Reporter.StartProgress("cutting", ProgressUnit.Seconds, span.Length.Seconds);
            progress.Report(span.Length.Seconds, span.Length.Seconds);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllBytes(outputPath, new byte[100]);
        return Task.FromResult(Result.Success<DownloadedClip, Error>(new DownloadedClip(outputPath, ByteSize.FromBytes(100))));
    }
}
