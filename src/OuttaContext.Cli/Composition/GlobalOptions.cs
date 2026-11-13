namespace OuttaContext.Cli.Composition;

/// <summary>
/// Spectre reads options after the command (`outta clip … -v`). People also type them first
/// (`outta -v clip …`), so leading --verbose/--quiet flags are moved to the end.
/// </summary>
public static class GlobalOptions
{
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "-v", "--verbose", "-q", "--quiet" };

    public static string[] MoveAfterCommand(string[] args)
    {
        int leading = args.TakeWhile(Flags.Contains).Count();
        bool hasCommand = leading < args.Length;
        return leading > 0 && hasCommand ? [.. args[leading..], .. args[..leading]] : args;
    }
}
