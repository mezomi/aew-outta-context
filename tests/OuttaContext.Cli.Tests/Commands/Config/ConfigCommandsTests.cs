namespace OuttaContext.Cli.Tests.Commands.Config;

public sealed class ConfigCommandsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task SetThenShow_Values_AreSavedAndShown()
    {
        await _cli.RunAsync("config", "set", "episode-length", "8:00");
        await _cli.RunAsync("config", "set", "max-clip-length", "1:00");

        (int code, string output) = await _cli.RunAsync("config", "show");

        code.ShouldBe(0);
        output.ShouldContain("8:00");
        output.ShouldContain("1:00");
        output.ShouldContain("Data folder");
    }

    [Fact]
    public async Task Set_UnknownKey_ListsTheKeys()
    {
        (int code, string output) = await _cli.RunAsync("config", "set", "bogus", "1");

        code.ShouldBe(1);
        output.ShouldContain("Settings you can change");
    }
}
