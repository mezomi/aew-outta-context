using OuttaContext.Core.Paths;
using OuttaContext.Infrastructure.FFmpeg;
using OuttaContext.Infrastructure.Media;
using OuttaContext.Infrastructure.Processes;
using OuttaContext.Infrastructure.YtDlp;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.IntegrationTests.Support;

/// <summary>The real yt-dlp/ffmpeg adapters, wired the way the app wires them.</summary>
public sealed class RealTools(TempDirectory temp)
{
    public ProcessRunner Runner { get; } = new();

    public ToolLocator Tools { get; } = new(new AppPaths(temp.File("app")));

    public YtDlpOptions Options { get; } = new();

    public List<string> Details { get; } = [];

    public FfmpegMediaProcessor Ffmpeg => new(Runner, Tools, Details.Add);

    public YtDlpVideoResolver Resolver => new(Runner, Tools, Options);

    public ClipDownloader Downloader => new(new YtDlpStreamFetcher(Runner, Tools, Options, Details.Add), Ffmpeg);
}
