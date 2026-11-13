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
}
