namespace OuttaContext.Infrastructure.Processes;

/// <summary>Everything needed to run an outside program.</summary>
/// <param name="FileName">The program, e.g. a full path to ffmpeg.</param>
/// <param name="Arguments">Each argument separately (never one joined string, so nothing needs quoting).</param>
public sealed record ProcessSpec(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>Extra environment variables. Secrets go here, never in <see cref="Arguments"/>.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>Text written to the program's input, then closed.</summary>
    public Maybe<string> StandardInput { get; init; }

    /// <summary>Called for each line the program writes to its output.</summary>
    public Action<string> OnOutputLine { get; init; } = _ => { };

    /// <summary>Called for each line the program writes to its error stream.</summary>
    public Action<string> OnErrorLine { get; init; } = _ => { };

    /// <summary>Stop the program if it prints nothing for this long. None: wait as long as it takes.</summary>
    public Maybe<TimeSpan> StallTimeout { get; init; }

    /// <summary>Let the program use the console directly (for browser sign-ins). Output is not captured.</summary>
    public bool Interactive { get; init; }
}
