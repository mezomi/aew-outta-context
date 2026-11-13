using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.Storage;

public sealed class RcloneBackendTests : IDisposable
{
    private const string Secret = "s3cr3t-value";

    private readonly TempDirectory _temp = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly InMemoryCredentialStore _keychain = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void RemoteName_StorageName_IsSafeForEnvironmentVariables()
    {
        Rclone.RemoteName("My-Drive").ShouldBe("outta_my_drive");
    }

    [Fact]
    public async Task SignInAsync_GoogleDrive_RunsInteractiveRcloneThenEncryptsTheConfig()
    {
        RcloneBackend drive = Backend(new StorageConfig("drive", StorageProviders.GoogleDrive, "AEW"));

        (await drive.SignInAsync(_ => string.Empty, CancellationToken.None)).ValueOrThrow();

        _runner.Runs[0].Arguments.ShouldBe(["--config", _temp.File("rclone.conf"), "config", "create", "outta_drive", "drive", "scope=drive"]);
        _runner.Runs[0].Interactive.ShouldBeTrue();
        string password = _keychain.Get(Rclone.ConfigPasswordKey)!;
        password.ShouldNotBeNullOrEmpty();
        _runner.Runs[1].StandardInput.ShouldBe($"{password}\n{password}\n");
        _runner.Runs.ShouldAllBe(run => run.Arguments.All(argument => !argument.Contains(password, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TestAsync_Backblaze_KeyTravelsOnlyInEnvironmentVariables()
    {
        // Arrange
        RcloneBackend b2 = Backend(new StorageConfig("b2", StorageProviders.Backblaze, "bucket/aew", new Dictionary<string, string> { ["account"] = "acc123" }));
        await b2.SignInAsync(_ => Secret, CancellationToken.None);

        // Act
        (await b2.TestAsync(CancellationToken.None)).ValueOrThrow();

        // Assert
        _runner.Runs.Select(run => run.Arguments[2]).ShouldBe(["mkdir", "rcat", "deletefile"]);
        _runner.Runs[0].Environment["RCLONE_CONFIG_OUTTA_B2_KEY"].ShouldBe(Secret);
        _runner.Runs[0].Environment["RCLONE_CONFIG_OUTTA_B2_ACCOUNT"].ShouldBe("acc123");
        _runner.Runs.ShouldAllBe(run => run.Arguments.All(argument => !argument.Contains(Secret, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UploadAsync_JsonLog_ReportsProgress()
    {
        string file = _temp.Write("Episode 3.mp4", new string('x', 1000));
        _runner.Respond = _ => new FakeRun { ErrorLines = ["{\"level\":\"info\",\"stats\":{\"bytes\":500,\"totalBytes\":1000}}", "not json"] };
        var progress = new List<(double, double?)>();

        string where = (await Backend(new StorageConfig("drive", StorageProviders.GoogleDrive, "AEW")).UploadAsync(file, "Episode 3", (d, t) => progress.Add((d, t)), CancellationToken.None)).ValueOrThrow();

        where.ShouldBe("outta_drive:AEW/Episode 3/Episode 3.mp4");
        progress.ShouldBe([(500, 1000), (1000, 1000)]);
    }

    [Fact]
    public async Task UploadAsync_ExpiredLogin_SaysToSignInAgain()
    {
        string file = _temp.Write("a.mp4");
        _runner.Respond = _ => new FakeRun(1) { ErrorLines = ["{\"level\":\"error\",\"msg\":\"oauth2: invalid_grant\"}"] };

        var result = await Backend(new StorageConfig("drive", StorageProviders.GoogleDrive, "AEW")).UploadAsync(file, string.Empty, (_, _) => { }, CancellationToken.None);

        result.Error.Message.ShouldContain("was refused");
        result.Error.Hint.ShouldContain("add it again");
    }

    [Fact]
    public async Task ForgetAsync_Backblaze_RemovesTheStoredKey()
    {
        RcloneBackend b2 = Backend(new StorageConfig("b2", StorageProviders.Backblaze, "bucket"));
        await b2.SignInAsync(_ => Secret, CancellationToken.None);

        await b2.ForgetAsync(CancellationToken.None);

        _keychain.Secrets.ShouldBeEmpty();
    }

    private RcloneBackend Backend(StorageConfig config) =>
        new(config, new Rclone(_runner, InstalledTools.Only(_temp, "rclone"), _keychain, _temp.File("rclone.conf")));
}
