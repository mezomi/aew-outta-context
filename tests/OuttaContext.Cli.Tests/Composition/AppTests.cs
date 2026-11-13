namespace OuttaContext.Cli.Tests.Composition;

public sealed class AppTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task Help_ListsEveryCommandGroup()
    {
        (_, string output) = await _cli.RunAsync("--help");

        foreach (string command in new[] { "clip", "scrape", "episode", "storage", "upload", "config", "doctor", "update" })
        {
            output.ShouldContain(command);
        }
    }

    [Fact]
    public async Task VerboseFirst_LikeTheOldCli_StillWorks()
    {
        (int code, _) = await _cli.RunAsync("-v", "config", "show");

        code.ShouldBe(0);
    }

    [Fact]
    public async Task BrokenSettingsFile_PrintsWhereItIsInsteadOfCrashing()
    {
        Directory.CreateDirectory(_cli.Paths.DataDirectory);
        File.WriteAllText(_cli.Paths.SettingsFile, "max_quality = 999");

        (int code, string output) = await _cli.RunAsync("episode", "status");

        code.ShouldBe(1);
        output.ShouldContain("error: Can't read");
        output.ShouldContain("max_quality");
        output.ShouldContain("hint: Fix or delete the file");
    }

    [Fact]
    public async Task BrokenSettingsFile_ConfigSetStillExplainsTheProblem()
    {
        Directory.CreateDirectory(_cli.Paths.DataDirectory);
        File.WriteAllText(_cli.Paths.SettingsFile, "max_quality = [");

        (int code, string output) = await _cli.RunAsync("config", "set", "max-quality", "720");

        code.ShouldBe(1);
        output.ShouldContain("not valid TOML");
    }
}
