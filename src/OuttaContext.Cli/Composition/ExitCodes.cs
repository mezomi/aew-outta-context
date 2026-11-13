namespace OuttaContext.Cli.Composition;

/// <summary>What `outta` returns to the shell.</summary>
public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>Something the user can fix (printed as error + hint).</summary>
    public const int Failure = 1;

    /// <summary>Wrong options or arguments.</summary>
    public const int UsageError = 2;

    /// <summary>Ctrl+C.</summary>
    public const int Cancelled = 130;
}
