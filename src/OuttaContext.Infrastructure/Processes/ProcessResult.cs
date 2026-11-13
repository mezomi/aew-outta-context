namespace OuttaContext.Infrastructure.Processes;

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
