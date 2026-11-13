namespace OuttaContext.Infrastructure.Processes;

/// <summary>Everything needed to run an outside program.</summary>
/// <param name="FileName">The program, e.g. a full path to ffmpeg.</param>
/// <param name="Arguments">Each argument separately (never one joined string, so nothing needs quoting).</param>
public sealed record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>Extra environment variables. Secrets go here, never in <see cref="Arguments"/>.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>Text written to the program's input, then closed.</summary>
    public string? StandardInput { get; init; }

    /// <summary>Called for each line the program writes to its output.</summary>
    public Action<string>? OnOutputLine { get; init; }

    /// <summary>Called for each line the program writes to its error stream.</summary>
    public Action<string>? OnErrorLine { get; init; }

    /// <summary>Stop the program if it prints nothing for this long. Null: wait forever.</summary>
    public TimeSpan? StallTimeout { get; init; }

    /// <summary>Let the program use the console directly (for browser sign-ins). Output is not captured.</summary>
    public bool Interactive { get; init; }
}

/// <summary>How a program run ended.</summary>
public sealed record ProcessResult(int ExitCode, string Output, string Error)
{
    /// <summary>The program could not be started (not installed / not on PATH).</summary>
    public bool NotFound { get; init; }

    /// <summary>The program printed nothing for longer than the stall timeout and was stopped.</summary>
    public bool Stalled { get; init; }

    public bool Succeeded => ExitCode == 0 && !NotFound && !Stalled;

    /// <summary>The last few lines of the error stream, for messages.</summary>
    public string ErrorTail(int lines = 5) =>
        string.Join(
            System.Environment.NewLine,
            Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).TakeLast(lines));
}

/// <summary>Runs outside programs. Faked in tests.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken);
}
