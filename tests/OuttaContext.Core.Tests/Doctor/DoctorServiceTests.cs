using OuttaContext.Core.Doctor;
using OuttaContext.Core.Results;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Core.Tests.Doctor;

public sealed class DoctorServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private readonly TempDirectory _temp = new();
    private readonly FakeMediaProcessor _media = new();
    private readonly FakeEnvironment _environment = new();
    private readonly FakeStorages _storages = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("2026.10.01", 43)]
    [InlineData("2026.11.12.232", 1)]
    [InlineData("nightly", null)]
    public void YtDlpAgeDays_Version_ReturnsDaysSinceRelease(string version, int? expected)
    {
        DoctorService.YtDlpAgeDays(version, Today).ShouldBe(expected);
    }

    [Fact]
    public async Task RunAsync_EverythingInstalled_AllRequiredChecksPass()
    {
        IReadOnlyDictionary<string, DoctorCheck> checks = await Run();

        checks["ffmpeg"].Message.ShouldBe("7.1");
        foreach (string name in new[] { "ffmpeg", "yt-dlp", "impersonation", "js runtime", "keychain", "rclone" })
        {
            checks[name].State.ShouldBe(CheckState.Ok, name);
        }
    }

    [Fact]
    public async Task RunAsync_MissingTools_EveryProblemComesWithAFix()
    {
        // Arrange
        _media.IsAvailable = false;
        _environment.Programs.Clear();
        _environment.YtDlpVersion = "2025.01.01";
        _environment.Clients = [];
        _storages.Configs.Add(new StorageConfig("drive", StorageProviders.GoogleDrive, "AEW"));

        // Act
        IReadOnlyDictionary<string, DoctorCheck> checks = await Run(testStorage: false);

        // Assert
        checks["ffmpeg"].State.ShouldBe(CheckState.Fail);
        checks["ffmpeg"].Fix.ShouldContain("winget install ffmpeg");
        checks["yt-dlp"].State.ShouldBe(CheckState.Warn);
        checks["impersonation"].Message.ShouldContain("Dailymotion");
        checks["js runtime"].State.ShouldBe(CheckState.Warn);
        checks["rclone"].State.ShouldBe(CheckState.Fail);
        checks.Values.Where(c => c.State != CheckState.Ok && c.Name != "outta").ShouldAllBe(c => c.Fix.Length > 0);
    }

    [Fact]
    public async Task RunAsync_NoYtDlp_FailsWithUpdateHint()
    {
        _environment.YtDlpVersion = null;

        IReadOnlyDictionary<string, DoctorCheck> checks = await Run();

        checks["yt-dlp"].State.ShouldBe(CheckState.Fail);
        checks["yt-dlp"].Fix.ShouldContain("outta update");
    }

    [Fact]
    public async Task RunAsync_StorageLogins_AreTested()
    {
        _storages.Configs.Add(new StorageConfig("good", StorageProviders.LocalFolder, "ok"));
        _storages.Configs.Add(new StorageConfig("bad", StorageProviders.LocalFolder, "gone"));

        IReadOnlyDictionary<string, DoctorCheck> checks = await Run();

        checks["storage good"].State.ShouldBe(CheckState.Ok);
        checks["storage bad"].State.ShouldBe(CheckState.Fail);
    }

    private async Task<IReadOnlyDictionary<string, DoctorCheck>> Run(bool testStorage = true)
    {
        var doctor = new DoctorService(_media, _environment, new OuttaSettings { ClipFolder = _temp.File("clips") }, _storages, new InMemoryCredentialStore());
        return (await doctor.RunAsync(testStorage, CancellationToken.None)).ToDictionary(check => check.Name);
    }

    private sealed class FakeEnvironment : IEnvironmentInspector
    {
        public HashSet<string> Programs { get; } = ["deno", "rclone"];

        public string? YtDlpVersion { get; set; } = "2026.11.01";

        public IReadOnlyList<string> Clients { get; set; } = ["chrome", "firefox"];

        public DateOnly Today => DoctorServiceTests.Today;

        public string? FindProgram(string name) => Programs.Contains(name) ? "/bin/" + name : null;

        public Task<string?> GetYtDlpVersionAsync(CancellationToken cancellationToken) => Task.FromResult(YtDlpVersion);

        public Task<IReadOnlyList<string>> GetImpersonationClientsAsync(CancellationToken cancellationToken) => Task.FromResult(Clients);
    }

    /// <summary>Storages whose test passes when their folder is "ok".</summary>
    private sealed class FakeStorages : IStorageRegistry
    {
        public List<StorageConfig> Configs { get; } = [];

        public IReadOnlyList<StorageConfig> List() => Configs;

        public StorageConfig Get(string name) => Configs.Single(c => c.Name == name);

        public void Add(StorageConfig config) => Configs.Add(config);

        public StorageConfig Remove(string name) => throw new NotSupportedException();

        public IStorageBackend Backend(StorageConfig config) => new FakeBackend(config);

        private sealed class FakeBackend(StorageConfig config) : IStorageBackend
        {
            public StorageConfig Config => config;

            public Task<ToolResult<string>> UploadAsync(string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<ToolResult<Done>> TestAsync(CancellationToken cancellationToken) =>
                Task.FromResult(config.Folder == "ok"
                    ? ToolResult.Success()
                    : ToolResult.Failure(new ToolError("local folder", "Folder gone does not exist.", "Connect the drive.")));

            public Task<ToolResult<Done>> ForgetAsync(CancellationToken cancellationToken) => Task.FromResult(ToolResult.Success());
        }
    }
}
