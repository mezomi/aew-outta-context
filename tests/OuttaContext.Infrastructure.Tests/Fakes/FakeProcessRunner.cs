using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.Tests.Fakes;

/// <summary>
/// Pretends to run programs. <see cref="Respond"/> decides what each run "prints" and returns;
/// every <see cref="ProcessSpec"/> is recorded so tests can check the exact command.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Runs { get; } = [];

    /// <summary>Called for each run. Default: success with no output.</summary>
    public Func<ProcessSpec, FakeRun> Respond { get; set; } = _ => new FakeRun();

    public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        Runs.Add(spec);
        FakeRun run = Respond(spec);
        run.SideEffect?.Invoke(spec);
        foreach (string line in run.OutputLines)
        {
            spec.OnOutputLine?.Invoke(line);
        }

        foreach (string line in run.ErrorLines)
        {
            spec.OnErrorLine?.Invoke(line);
        }

        return Task.FromResult(new ProcessResult(run.ExitCode, string.Join('\n', run.OutputLines), string.Join('\n', run.ErrorLines))
        {
            NotFound = run.NotFound,
            Stalled = run.Stalled,
        });
    }

    public IReadOnlyList<string> LastArguments => Runs[^1].Arguments;
}

/// <summary>What a fake program run does: its output, exit code, and any files it "writes".</summary>
public sealed record FakeRun(int ExitCode = 0)
{
    public IReadOnlyList<string> OutputLines { get; init; } = [];

    public IReadOnlyList<string> ErrorLines { get; init; } = [];

    public bool NotFound { get; init; }

    public bool Stalled { get; init; }

    /// <summary>Runs before output is sent, e.g. to create the file the real program would write.</summary>
    public Action<ProcessSpec>? SideEffect { get; init; }

    public static FakeRun Failing(params string[] errorLines) => new(1) { ErrorLines = errorLines };

    /// <summary>The value after an option, e.g. ValueAfter(spec, "--output").</summary>
    public static string ValueAfter(ProcessSpec spec, string option) => spec.Arguments[spec.Arguments.ToList().IndexOf(option) + 1];
}
