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
            spec.OnOutputLine(line);
        }

        foreach (string line in run.ErrorLines)
        {
            spec.OnErrorLine(line);
        }

        return Task.FromResult(new ProcessResult(run.ExitCode, string.Join('\n', run.OutputLines), string.Join('\n', run.ErrorLines))
        {
            NotFound = run.NotFound,
            Stalled = run.Stalled,
        });
    }

    public IReadOnlyList<string> LastArguments => Runs[^1].Arguments;
}
