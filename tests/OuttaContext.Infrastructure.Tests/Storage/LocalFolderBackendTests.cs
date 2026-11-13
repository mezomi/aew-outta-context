using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Storage;

public sealed class LocalFolderBackendTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task UploadAsync_ConnectedFolder_CopiesIntoTheSubfolderWithProgress()
    {
        // Arrange
        Directory.CreateDirectory(_temp.File("nas"));
        string file = _temp.Write("a.mp4", "0123456789");
        var progress = new List<double>();

        // Act
        string where = (await Backend().UploadAsync(file, "Episode 1/clips", (done, _) => progress.Add(done), Ct)).ShouldSucceed().Location;

        // Assert
        File.ReadAllText(where).ShouldBe("0123456789");
        where.ShouldContain(Path.Combine("Episode 1", "clips"));
        progress[^1].ShouldBe(10);
        Directory.GetFiles(_temp.File("nas"), "*.part", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task UploadAsync_FolderNotConnected_SaysToPlugInTheDrive()
    {
        var result = await Backend().UploadAsync(_temp.Write("a.mp4"), string.Empty, (_, _) => { }, Ct);

        result.ShouldFailWith("local.unavailable").Hint.ShouldContain("Plug in the drive");
    }

    [Fact]
    public async Task TestAsync_ConnectedFolder_Succeeds_MissingFolder_Fails()
    {
        (await Backend().TestAsync(Ct)).ShouldFailWith("local.missing");
        Directory.CreateDirectory(_temp.File("nas"));
        (await Backend().TestAsync(Ct)).ShouldSucceed();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LocalFolderBackend Backend() => new(new StorageConfig(Values.Storage("nas"), StorageProvider.LocalFolder, _temp.File("nas")));
}
