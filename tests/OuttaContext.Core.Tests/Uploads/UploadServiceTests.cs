using OuttaContext.Core.Errors;
using OuttaContext.Core.Library;
using OuttaContext.Core.Results;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.Core.Uploads;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Uploads;

public sealed class UploadServiceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly InMemoryLibraryRepository _repository = new();
    private readonly RecordingReporter _reporter = new();
    private readonly CopyingStorages _storages;

    public UploadServiceTests()
    {
        _storages = new CopyingStorages(_temp.File("remote"));
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task UploadFileAsync_NoStorageChosenOrDefault_ThrowsStorageNotConfigured()
    {
        string file = _temp.Write("a.mp4");

        await Should.ThrowAsync<StorageNotConfiguredException>(() => Service(defaultStorage: null).UploadFileAsync(file, null, CancellationToken.None));
    }

    [Fact]
    public async Task UploadFileAsync_SameFileTwice_SkipsTheSecondUpload()
    {
        string file = _temp.Write("a.mp4", "abc");
        UploadService service = Service();

        await service.UploadFileAsync(file, null, CancellationToken.None);
        await service.UploadFileAsync(file, null, CancellationToken.None);

        _storages.Uploads.Count.ShouldBe(1);
        _reporter.Texts("success")[^1].ShouldStartWith("Already uploaded a.mp4 to nas");
    }

    [Fact]
    public async Task UploadEpisodeAsync_BuiltEpisode_UploadsVideoAndClipsIntoItsFolder()
    {
        // Arrange
        string clipFile = _temp.Write("Table spot.mp4", "c");
        Clip clip = _repository.AddClip(new NewClip("Table spot", "youtube", "u", 0, 1, 1, clipFile, null));
        Episode episode = _repository.MarkBuilt(_repository.CreateEpisode("Episode 3", 600).Id, _temp.Write("Episode 3.mp4", "e"));

        // Act
        await Service().UploadEpisodeAsync(episode, [clip], null, CancellationToken.None);

        // Assert
        _storages.Uploads.ShouldBe([("Episode 3", "Episode 3.mp4"), ("Episode 3/clips", "Table spot.mp4")]);
    }

    [Fact]
    public async Task UploadEpisodeAsync_NotBuiltYet_ThrowsWithBuildHint()
    {
        Episode episode = _repository.CreateEpisode("Episode 3", 600);

        var error = await Should.ThrowAsync<OuttaException>(() => Service().UploadEpisodeAsync(episode, [], null, CancellationToken.None));

        error.Hint.ShouldContain("outta episode build");
    }

    private UploadService Service(string? defaultStorage = "nas") =>
        new(_storages, _repository, new OuttaSettings { DefaultStorage = defaultStorage }, _reporter);

    /// <summary>One storage named "nas" that records what it was asked to upload.</summary>
    private sealed class CopyingStorages(string root) : IStorageRegistry, IStorageBackend
    {
        public List<(string Subfolder, string FileName)> Uploads { get; } = [];

        public StorageConfig Config { get; } = new("nas", StorageProviders.LocalFolder, root);

        public IReadOnlyList<StorageConfig> List() => [Config];

        public StorageConfig Get(string name) => name == "nas" ? Config : throw new StorageNotConfiguredException($"No storage named \"{name}\".");

        public void Add(StorageConfig config) => throw new NotSupportedException();

        public StorageConfig Remove(string name) => throw new NotSupportedException();

        public IStorageBackend Backend(StorageConfig config) => this;

        public Task<ToolResult<string>> UploadAsync(string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken)
        {
            Uploads.Add((subfolder, Path.GetFileName(localPath)));
            return Task.FromResult(ToolResult<string>.Success($"{subfolder}/{Path.GetFileName(localPath)}"));
        }

        public Task<ToolResult<Done>> TestAsync(CancellationToken cancellationToken) => Task.FromResult(ToolResult.Success());

        public Task<ToolResult<Done>> ForgetAsync(CancellationToken cancellationToken) => Task.FromResult(ToolResult.Success());
    }
}
