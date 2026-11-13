using OuttaContext.Core.Common;
using OuttaContext.Core.Episodes;
using OuttaContext.Core.Library;
using OuttaContext.Core.Media;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Episodes;

public sealed class BuildEpisodeHandlerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly FakeMediaProcessor _media = new();
    private readonly RecordingReporter _reporter = new();
    private readonly FakeEpisodeUploader _uploader = new();
    private readonly EpisodeService _episodes;
    private readonly OuttaSettings _settings;

    public BuildEpisodeHandlerTests()
    {
        _settings = new OuttaSettings { ClipFolder = _temp.File("clips") };
        _episodes = new EpisodeService(_repository, _settings);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task HandleAsync_ThreeClips_JoinsThemAndOpensTheNextEpisode()
    {
        // Arrange
        AddClipOnDisk("a");
        AddClipOnDisk("b");
        AddClipOnDisk("c");

        // Act
        BuildEpisodeResult result = (await Handler().HandleAsync(new BuildEpisodeRequest(), Ct)).ShouldSucceed();

        // Assert
        _media.Normalized.Select(n => Path.GetFileName(n.Source)).ShouldBe(["a.mp4", "b.mp4", "c.mp4"]);
        result.OutputPath.ShouldBe(Path.Combine(_settings.ClipFolder, "Episodes", "Episode 1.mp4"));
        result.Duration.Seconds.ShouldBe(60);
        result.Episode.IsBuilt.ShouldBeTrue();
        result.NextEpisode.ShouldHaveValue().Name.Value.ShouldBe("Episode 2");
        _reporter.Texts("step")[0].ShouldBe("Building \"Episode 1\": 3 clips, 1:00");
        _reporter.Texts("success")[^1].ShouldBe("Opened \"Episode 2\" for new clips");
    }

    [Fact]
    public async Task HandleAsync_RebuildAfterReorder_ReusesConvertedClips()
    {
        // Arrange
        AddClipOnDisk("a");
        Clip second = AddClipOnDisk("b");
        (await Handler().HandleAsync(new BuildEpisodeRequest(), Ct)).ShouldSucceed();
        _episodes.Move(second.Id, 1).ShouldSucceed();

        // Act
        (await Handler().HandleAsync(new BuildEpisodeRequest("Episode 1"), Ct)).ShouldSucceed();

        // Assert
        _media.Normalized.Count.ShouldBe(2);
        _media.Concats[^1].Paths.Select(p => Path.GetFileName(p).Split('-')[0]).ShouldBe(["clip2", "clip1"]);
        _reporter.Texts("step").ShouldContain(s => s.Contains("using converted copy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandleAsync_NoClips_FailsWithEpisodeEmpty()
    {
        (await Handler().HandleAsync(new BuildEpisodeRequest(), Ct)).ShouldFailWith("episode.empty");
    }

    [Fact]
    public async Task HandleAsync_UnknownEpisode_Fails()
    {
        (await Handler().HandleAsync(new BuildEpisodeRequest("Nope"), Ct)).ShouldFailWith("episode.not_found");
    }

    [Fact]
    public async Task HandleAsync_ClipFileMissing_FailsBeforeConverting()
    {
        Clip clip = AddClipOnDisk("a");
        File.Delete(clip.FilePath);

        var result = await Handler().HandleAsync(new BuildEpisodeRequest(), Ct);

        result.ShouldFailWith("episode.clip_file_missing").Message.ShouldContain("Clip 1");
        _media.Normalized.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoFfmpeg_FailsWithInstallHint()
    {
        AddClipOnDisk("a");
        _media.IsAvailable = false;

        var result = await Handler().HandleAsync(new BuildEpisodeRequest(), Ct);

        result.ShouldFailWith("ffmpeg.missing").Hint.ShouldContain("winget install ffmpeg");
    }

    [Fact]
    public async Task HandleAsync_ConvertFails_StopsAndKeepsTheEpisodeOpen()
    {
        AddClipOnDisk("a");
        _media.NormalizeFailure = new Error("ffmpeg.failed", "ffmpeg failed (exit 1)", "Rerun with -v");

        var result = await Handler().HandleAsync(new BuildEpisodeRequest(), Ct);

        result.ShouldFailWith("ffmpeg.failed");
        _media.Concats.ShouldBeEmpty();
        _episodes.Current().IsBuilt.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_CleanLocalWithoutUpload_IsRefused()
    {
        AddClipOnDisk("a");

        (await Handler().HandleAsync(new BuildEpisodeRequest(CleanLocal: true), Ct)).ShouldFailWith("episode.clean_local");
    }

    [Fact]
    public async Task HandleAsync_UploadWithCleanLocal_UploadsThenDeletesLocalClips()
    {
        Clip clip = AddClipOnDisk("a");

        var result = await Handler().HandleAsync(new BuildEpisodeRequest(Upload: true, Storage: "drive", CleanLocal: true), Ct);

        result.ShouldSucceed();
        _uploader.Uploads.Select(u => (u.Episode.Name.Value, u.Clips.Count, u.Storage)).ShouldBe([("Episode 1", 1, (string?)"drive")]);
        File.Exists(clip.FilePath).ShouldBeFalse();
        File.Exists(result.Value.OutputPath).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_UploadFails_KeepsLocalClips()
    {
        Clip clip = AddClipOnDisk("a");
        _uploader.Failure = new Error("rclone.upload_failed", "Upload failed", "Check your connection");

        var result = await Handler().HandleAsync(new BuildEpisodeRequest(Upload: true, CleanLocal: true), Ct);

        result.ShouldFailWith("rclone.upload_failed");
        File.Exists(clip.FilePath).ShouldBeTrue();
    }

    private BuildEpisodeHandler Handler() =>
        new(_episodes, _media, new EpisodeCache(new AppPaths(_temp.File("data")), new NormalizeTarget()), _settings, _uploader, _reporter);

    private Clip AddClipOnDisk(string title)
    {
        string path = _temp.Write(Path.Combine("clips", title + ".mp4"), title);
        Clip clip = _repository.AddClip(Values.NewClip(title, 20, path));
        _episodes.AddClip(clip).ShouldSucceed();
        return _repository.FindClip(clip.Id).ShouldHaveValue();
    }
}
