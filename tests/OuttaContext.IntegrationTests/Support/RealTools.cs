using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.FFmpeg;
using OuttaContext.Infrastructure.Media;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.YtDlp;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.IntegrationTests.Support;

/// <summary>The real yt-dlp/ffmpeg adapters, wired the way the app wires them.</summary>
public sealed class RealTools
{
    public RealTools(TempDirectory temp, RecordingReporter reporter)
    {
        Reporter = reporter;
        Tools = new ToolLocator(new AppPaths(temp.File("app")));
        Options = new YtDlpOptions(reporter);
    }

    public RecordingReporter Reporter { get; }

    public ProcessRunner Runner { get; } = new();

    public ToolLocator Tools { get; }

    public YtDlpOptions Options { get; }

    public FfmpegMediaProcessor Ffmpeg => new(Runner, Tools, Reporter);

    public YtDlpVideoResolver Resolver => new(Runner, Tools, Options, Reporter);

    public ClipDownloader Downloader => new(new YtDlpStreamFetcher(Runner, Tools, Options, Reporter), Ffmpeg, Reporter);
}
