namespace OuttaContext.Cli.Tests.Commands.Doctor;

public sealed class DoctorCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public async Task Doctor_EverythingInstalled_SaysReady()
    {
        await _cli.WithClipFolderAsync();

        (int code, string output) = await _cli.RunAsync("doctor", "--no-storage");

        code.ShouldBe(0, output);
        output.ShouldContain("impersonation");
        output.ShouldContain("All required tools are ready.");
    }

    [Fact]
    public async Task Doctor_NoFfmpeg_FailsWithTheFix()
    {
        _cli.Media.IsAvailable = false;

        (int code, string output) = await _cli.RunAsync("doctor", "--no-storage");

        code.ShouldBe(1);
        output.ShouldContain("winget install ffmpeg");
    }
}
