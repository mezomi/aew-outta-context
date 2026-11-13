using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.Tests.Processes;

public sealed class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new();

    [Fact]
    public async Task RunAsync_RealProgram_CapturesOutputLines()
    {
        var lines = new List<string>();

        ProcessResult result = await _runner.RunAsync(
            new ProcessSpec("dotnet", ["--version"]) { OnOutputLine = lines.Add }, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        lines.ShouldNotBeEmpty();
        result.Output.ShouldContain(lines[0]);
    }

    [Fact]
    public async Task RunAsync_ProgramNotInstalled_ReturnsNotFoundInsteadOfThrowing()
    {
        ProcessResult result = await _runner.RunAsync(new ProcessSpec("outta-no-such-program", []), TestContext.Current.CancellationToken);

        result.NotFound.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void ErrorTail_LongErrorOutput_KeepsTheLastLines()
    {
        var result = new ProcessResult(1, string.Empty, "one\ntwo\nthree\nfour");

        result.ErrorTail(2).ShouldBe($"three{Environment.NewLine}four");
    }
}
