using OuttaContext.Core.Clips;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.TestKit.Builders;

/// <summary>
/// Builds a <see cref="MakeClipHandler"/> wired to fakes. Change only what a test cares about:
/// <c>var builder = new MakeClipHandlerBuilder(temp) { Settings = ... };</c>
/// </summary>
public sealed class MakeClipHandlerBuilder
{
    private EpisodeService? _episodes;

    public MakeClipHandlerBuilder(TempDirectory temp)
    {
        Settings = new OuttaSettings { ClipFolder = temp.File("clips") };
        Downloader = new FakeClipDownloader(Reporter);
    }

    public RecordingReporter Reporter { get; } = new();

    public FakeVideoResolver Resolver { get; } = new();

    public FakeClipDownloader Downloader { get; }

    public FakeMediaProcessor Media { get; } = new();

    public InMemoryLibraryRepository Repository { get; } = new();

    public FakeClipUploader Uploader { get; } = new();

    public OuttaSettings Settings { get; set; }

    public EpisodeService Episodes => _episodes ??= new EpisodeService(Repository, Settings);

    public MakeClipHandler Build() =>
        new(SourceRegistry.Default, Resolver, Downloader, Media, Repository, Episodes, Settings, Uploader, Reporter);
}
