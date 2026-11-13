using OuttaContext.Core.Library;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.Core.Time;
using OuttaContext.Core.Uploads;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Uploads;

public sealed class UploadServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly RecordingReporter _reporter = new();
    private readonly InMemoryStorageRegistry _storages = new();
    private readonly FakeStorageBackends _backends = new();

    public UploadServiceTests()
    {
        _storages.Add(new StorageConfig(Values.Storage("nas"), StorageProvider.LocalFolder, "remote")).ShouldSucceed();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task UploadFileAsync_NoStorageChosenOrDefault_FailsWithNoDefault()
    {
        string file = _temp.Write("a.mp4");

        var result = await Service(defaultStorage: null).UploadFileAsync(file, null, Ct);

        result.ShouldFailWith("storage.no_default").Hint.ShouldContain("outta storage add");
    }

    [Fact]
    public async Task UploadFileAsync_UnknownStorage_Fails()
    {
        string file = _temp.Write("a.mp4");

        (await Service().UploadFileAsync(file, "elsewhere", Ct)).ShouldFailWith("storage.not_found");
    }

    [Fact]
    public async Task UploadFileAsync_MissingFile_FailsWithoutUploading()
    {
        (await Service().UploadFileAsync(_temp.File("nope.mp4"), null, Ct)).ShouldFailWith("upload.file_missing");

        _backends.Uploads.ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadFileAsync_SameFileTwice_SkipsTheSecondUpload()
    {
        string file = _temp.Write("a.mp4", "abc");
        UploadService service = Service();

        (await service.UploadFileAsync(file, null, Ct)).ShouldSucceed();
        (await service.UploadFileAsync(file, null, Ct)).ShouldSucceed();

        _backends.Uploads.Count.ShouldBe(1);
        _reporter.Texts("success")[^1].ShouldStartWith("Already uploaded a.mp4 to nas");
    }

    [Fact]
    public async Task UploadFileAsync_BackendFails_ReturnsItsErrorAndRecordsNothing()
    {
        _storages.Add(new StorageConfig(Values.Storage("usb"), StorageProvider.LocalFolder, FakeStorageBackends.BrokenFolder)).ShouldSucceed();
        string file = _temp.Write("a.mp4");

        (await Service().UploadFileAsync(file, "usb", Ct)).ShouldFailWith("local.missing");

        _repository.FindUpload(file, "usb", "a.mp4").HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public async Task UploadEpisodeAsync_BuiltEpisode_UploadsVideoAndClipsIntoItsFolder()
    {
        // Arrange
        Clip clip = _repository.AddClip(Values.NewClip("Table spot", 1, _temp.Write("Table spot.mp4", "c")));
        Episode created = _repository.CreateEpisode(Values.Episode("Episode 3"), VideoTime.FromTrusted(600));
        Episode episode = _repository.MarkBuilt(created.Id, _temp.Write("Episode 3.mp4", "e"));

        // Act
        var result = await Service().UploadEpisodeAsync(episode, [clip], null, Ct);

        // Assert
        result.ShouldSucceed().Count.ShouldBe(2);
        _backends.Uploads.Select(u => (u.Subfolder, u.FileName)).ShouldBe([("Episode 3", "Episode 3.mp4"), ("Episode 3/clips", "Table spot.mp4")]);
    }

    [Fact]
    public async Task UploadEpisodeAsync_NotBuiltYet_FailsWithBuildHint()
    {
        Episode episode = _repository.CreateEpisode(Values.Episode("Episode 3"), VideoTime.FromTrusted(600));

        var result = await Service().UploadEpisodeAsync(episode, [], null, Ct);

        result.ShouldFailWith("upload.not_built").Hint.ShouldContain("outta episode build");
    }

    private UploadService Service(string? defaultStorage = "nas") =>
        new(
            _storages,
            _backends,
            _repository,
            new OuttaSettings { DefaultStorage = defaultStorage is null ? Maybe<StorageName>.None : Values.Storage(defaultStorage) },
            _reporter);
}
