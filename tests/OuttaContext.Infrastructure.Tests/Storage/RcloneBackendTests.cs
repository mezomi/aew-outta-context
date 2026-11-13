using OuttaContext.Core.Paths;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.TestKit.Assertions;
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
        Rclone.RemoteName(Values.Storage("My-Drive")).ShouldBe("outta_my_drive");
    }

    [Fact]
    public async Task SignInAsync_GoogleDrive_RunsInteractiveRcloneThenEncryptsTheConfig()
    {
        RcloneBackend drive = Backend(Drive);

        (await drive.SignInAsync(_ => string.Empty, Ct)).ShouldSucceed();

        _runner.Runs[0].Arguments.ShouldBe(["--config", _temp.File("rclone.conf"), "config", "create", "outta_drive", "drive", "scope=drive"]);
        _runner.Runs[0].Interactive.ShouldBeTrue();
        string password = _keychain.Get(Rclone.ConfigPasswordKey).ShouldHaveValue();
        password.ShouldNotBeNullOrEmpty();
        _runner.Runs[1].StandardInput.ShouldHaveValue().ShouldBe($"{password}\n{password}\n");
        _runner.Runs.ShouldAllBe(run => run.Arguments.All(argument => !argument.Contains(password, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task TestAsync_Backblaze_KeyTravelsOnlyInEnvironmentVariables()
    {
        // Arrange
        RcloneBackend b2 = Backend(new StorageConfig(Values.Storage("b2"), StorageProvider.Backblaze, "bucket/aew", new Dictionary<string, string> { ["account"] = "acc123" }));
        (await b2.SignInAsync(_ => Secret, Ct)).ShouldSucceed();

        // Act
        (await b2.TestAsync(Ct)).ShouldSucceed();

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

        string where = (await Backend(Drive).UploadAsync(file, "Episode 3", (d, t) => progress.Add((d, t)), Ct)).ShouldSucceed().Location;

        where.ShouldBe("outta_drive:AEW/Episode 3/Episode 3.mp4");
        progress.ShouldBe([(500, 1000), (1000, 1000)]);
    }

    [Fact]
    public async Task UploadAsync_ExpiredLogin_SaysToSignInAgain()
    {
        string file = _temp.Write("a.mp4");
        _runner.Respond = _ => new FakeRun(1) { ErrorLines = ["{\"level\":\"error\",\"msg\":\"oauth2: invalid_grant\"}"] };

        var result = await Backend(Drive).UploadAsync(file, string.Empty, (_, _) => { }, Ct);

        result.ShouldFailWith("rclone.refused").Hint.ShouldContain("add it again");
    }

    [Fact]
    public async Task ForgetAsync_Backblaze_RemovesTheStoredKey()
    {
        RcloneBackend b2 = Backend(new StorageConfig(Values.Storage("b2"), StorageProvider.Backblaze, "bucket"));
        (await b2.SignInAsync(_ => Secret, Ct)).ShouldSucceed();

        (await b2.ForgetAsync(Ct)).ShouldSucceed();

        _keychain.Secrets.ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_RcloneNotInstalled_SaysHowToInstall()
    {
        var backend = new RcloneBackend(Drive, new Rclone(_runner, InstalledTools.Only(_temp), _keychain, new AppPaths(_temp.Path)));

        var result = await backend.UploadAsync(_temp.Write("a.mp4"), string.Empty, (_, _) => { }, Ct);

        result.ShouldFailWith("rclone.missing").Hint.ShouldContain("winget install Rclone.Rclone");
    }

    private static StorageConfig Drive => new(Values.Storage("drive"), StorageProvider.GoogleDrive, "AEW");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RcloneBackend Backend(StorageConfig config) =>
        new(config, new Rclone(_runner, InstalledTools.Only(_temp, "rclone"), _keychain, new AppPaths(_temp.Path)));
}
