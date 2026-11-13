using OuttaContext.Core.Clips;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Sources;
using OuttaContext.Core.Uploads;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.TestKit.Builders;

/// <summary>
/// Builds a <see cref="MakeClipHandler"/> wired to fakes. Change only what a test cares about:
/// <c>var builder = new MakeClipHandlerBuilder(temp) { Settings = ... };</c>
/// </summary>
public sealed class MakeClipHandlerBuilder(TempDirectory temp)
{
    public FakeVideoResolver Resolver { get; } = new();

    public FakeClipDownloader Downloader { get; } = new();

    public FakeMediaProcessor Media { get; } = new();

    public InMemoryLibraryRepository Repository { get; } = new();

    public RecordingReporter Reporter { get; } = new();

    public IClipUploader? Uploader { get; set; }

    public OuttaSettings Settings { get; set; } = new() { ClipFolder = temp.File("clips") };

    public EpisodeService Episodes => _episodes ??= new EpisodeService(Repository, Settings.EpisodeLength);

    private EpisodeService? _episodes;

    public MakeClipHandler Build() =>
        new(new SourceRegistry(), Resolver, Downloader, Media, Repository, Episodes, Settings, Reporter, Uploader);
}
