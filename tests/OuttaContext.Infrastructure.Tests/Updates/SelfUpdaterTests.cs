using System.Security.Cryptography;
using System.Text;
using OuttaContext.Infrastructure.Tests.Fakes;
using OuttaContext.Infrastructure.Updates;
using OuttaContext.TestKit.Builders;
using OuttaContext.TestKit.Fakes;

namespace OuttaContext.Infrastructure.Tests.Updates;

public sealed class SelfUpdaterTests : IDisposable
{
    private const string Api = "https://api.github.com/repos/mezomi/aew-outta-context/releases/latest";
    private const string Download = "https://github.com/x/releases/download/v0.2.0/outta.exe";
    private static readonly byte[] NewBuild = Encoding.UTF8.GetBytes("new exe build");

    private readonly TempDirectory _temp = new();
    private readonly FakeHttpHandler _http = new();
    private readonly RecordingReporter _reporter = new();
    private readonly string _exe;

    public SelfUpdaterTests()
    {
        _exe = _temp.Write("outta.exe", "old exe build");
        _http.Bytes(Download, NewBuild);
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task UpdateAsync_NewerBuild_ReplacesTheExeAndKeepsTheOldOneAside()
    {
        Release(Hash(NewBuild));

        bool updated = (await Updater().UpdateAsync(checkOnly: false, CancellationToken.None)).ValueOrThrow();

        updated.ShouldBeTrue();
        File.ReadAllBytes(_exe).ShouldBe(NewBuild);
        File.ReadAllText(_exe + ".old").ShouldBe("old exe build");
        SelfUpdater.RemoveLeftover(_exe);
        File.Exists(_exe + ".old").ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_SameBuild_SaysUpToDate()
    {
        File.WriteAllBytes(_exe, NewBuild);
        Release(Hash(NewBuild));

        bool updated = (await Updater().UpdateAsync(checkOnly: false, CancellationToken.None)).ValueOrThrow();

        updated.ShouldBeFalse();
        _reporter.Texts("success").ShouldBe(["outta is up to date (v0.2.0)"]);
    }

    [Fact]
    public async Task UpdateAsync_CheckOnly_DoesNotDownload()
    {
        Release(Hash(NewBuild));

        await Updater().UpdateAsync(checkOnly: true, CancellationToken.None);

        File.ReadAllText(_exe).ShouldBe("old exe build");
        _http.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UpdateAsync_CorruptDownload_KeepsTheOldExe()
    {
        Release(new string('0', 64));

        var result = await Updater().UpdateAsync(checkOnly: false, CancellationToken.None);

        result.Error.Message.ShouldContain("checksum");
        File.ReadAllText(_exe).ShouldBe("old exe build");
        File.Exists(_exe + ".new").ShouldBeFalse();
    }

    private void Release(string digest) =>
        _http.Text(Api, $$"""{"tag_name":"v0.2.0","assets":[{"name":"outta.exe","browser_download_url":"{{Download}}","size":{{NewBuild.Length}},"digest":"sha256:{{digest}}"}]}""");

    private SelfUpdater Updater() => new(new GitHubReleases(_http.Client()), _exe, _reporter);

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
