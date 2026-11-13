using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.Tests.Fakes;

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
