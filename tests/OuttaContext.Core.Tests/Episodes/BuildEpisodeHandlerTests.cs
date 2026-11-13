using OuttaContext.Core.Episodes;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Uploads;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Episodes;

public sealed class BuildEpisodeHandlerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly FakeMediaProcessor _media = new();
    private readonly RecordingReporter _reporter = new();
    private readonly RecordingEpisodeUploader _uploader = new();
    private readonly EpisodeService _episodes;
    private readonly OuttaSettings _settings;

    public BuildEpisodeHandlerTests()
    {
        _episodes = new EpisodeService(_repository, 600);
        _settings = new OuttaSettings { ClipFolder = _temp.File("clips") };
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task HandleAsync_ThreeClips_JoinsThemAndOpensTheNextEpisode()
    {
        // Arrange
        AddClipOnDisk("a");
        AddClipOnDisk("b");
        AddClipOnDisk("c");

        // Act
        BuildEpisodeResult result = await Handler().HandleAsync(new BuildEpisodeRequest(), CancellationToken.None);

        // Assert
        _media.Normalized.Select(n => Path.GetFileName(n.Source)).ShouldBe(["a.mp4", "b.mp4", "c.mp4"]);
        result.OutputPath.ShouldBe(Path.Combine(_settings.ClipFolder, "Episodes", "Episode 1.mp4"));
        result.Duration.ShouldBe(60);
        result.Episode.IsBuilt.ShouldBeTrue();
        result.NextEpisode!.Name.ShouldBe("Episode 2");
        _reporter.Texts("step")[0].ShouldBe("Building \"Episode 1\": 3 clips, 1:00");
        _reporter.Texts("success")[^1].ShouldBe("Opened \"Episode 2\" for new clips");
    }

    [Fact]
    public async Task HandleAsync_RebuildAfterReorder_ReusesConvertedClips()
    {
        // Arrange
        AddClipOnDisk("a");
        Clip second = AddClipOnDisk("b");
        await Handler().HandleAsync(new BuildEpisodeRequest(), CancellationToken.None);
        _episodes.Move(second.Id, 1);

        // Act
        await Handler().HandleAsync(new BuildEpisodeRequest("Episode 1"), CancellationToken.None);

        // Assert
        _media.Normalized.Count.ShouldBe(2);
        _media.Concats[^1].Paths.Select(p => Path.GetFileName(p).Split('-')[0]).ShouldBe(["clip2", "clip1"]);
        _reporter.Texts("step").ShouldContain(s => s.Contains("using converted copy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_NoClips_ThrowsEpisodeEmpty()
    {
        await Should.ThrowAsync<EpisodeEmptyException>(() => Handler().HandleAsync(new BuildEpisodeRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ClipFileMissing_ThrowsBeforeConverting()
    {
        Clip clip = AddClipOnDisk("a");
        File.Delete(clip.FilePath);

        await Should.ThrowAsync<ClipFileMissingException>(() => Handler().HandleAsync(new BuildEpisodeRequest(), CancellationToken.None));

        _media.Normalized.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoFfmpeg_ThrowsWithInstallHint()
    {
        AddClipOnDisk("a");
        _media.IsAvailable = false;

        var error = await Should.ThrowAsync<OuttaException>(() => Handler().HandleAsync(new BuildEpisodeRequest(), CancellationToken.None));

        error.Hint.ShouldContain("winget install ffmpeg");
    }

    [Fact]
    public async Task HandleAsync_CleanLocalWithoutUpload_IsRefused()
    {
        AddClipOnDisk("a");

        await Should.ThrowAsync<OuttaException>(() => Handler().HandleAsync(new BuildEpisodeRequest(CleanLocal: true), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_UploadWithCleanLocal_UploadsThenDeletesLocalClips()
    {
        Clip clip = AddClipOnDisk("a");

        BuildEpisodeResult result = await Handler().HandleAsync(new BuildEpisodeRequest(Upload: true, Storage: "drive", CleanLocal: true), CancellationToken.None);

        _uploader.Uploaded.ShouldBe([("Episode 1", 1, (string?)"drive")]);
        File.Exists(clip.FilePath).ShouldBeFalse();
        File.Exists(result.OutputPath).ShouldBeTrue();
    }

    private BuildEpisodeHandler Handler() => new(_episodes, _media, _settings, _temp.File("cache"), _reporter, _uploader);

    private Clip AddClipOnDisk(string title)
    {
        string path = _temp.Write(Path.Combine("clips", title + ".mp4"), title);
        Clip clip = _repository.AddClip(new NewClip(title, "youtube", "u", 0, 20, 20, path, null));
        _episodes.AddClip(clip);
        return _repository.GetClip(clip.Id);
    }

    private sealed class RecordingEpisodeUploader : IEpisodeUploader
    {
        public List<(string Episode, int Clips, string? Storage)> Uploaded { get; } = [];

        public Task<IReadOnlyList<string>> UploadEpisodeAsync(Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken)
        {
            Uploaded.Add((episode.Name, clips.Count, storage));
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
    }
}
