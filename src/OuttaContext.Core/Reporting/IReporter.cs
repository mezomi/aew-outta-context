namespace OuttaContext.Core.Reporting;

/// <summary>How much the reporter prints.</summary>
public enum Verbosity
{
    Quiet,
    Normal,
    Verbose,
}

/// <summary>What a progress bar counts.</summary>
public enum ProgressUnit
{
    Bytes,
    Seconds,
    Segments,
}

/// <summary>
/// How use cases tell the user what is happening. The CLI shows it with Spectre.Console;
/// tests use a recording version and check the messages.
/// </summary>
public interface IReporter
{
    /// <summary>A step is starting ("→ Reading video info...").</summary>
    void Step(string message);

    /// <summary>Something finished ("✓ Saved ...").</summary>
    void Success(string message);

    /// <summary>Something the user should know but that is not an error.</summary>
    void Warn(string message);

    /// <summary>Extra detail, only shown with --verbose.</summary>
    void Detail(string message);

    /// <summary>A plain line of output.</summary>
    void Info(string message);

    /// <summary>Starts a progress bar; dispose it when the work is done.</summary>
    IProgressTask StartProgress(string description, ProgressUnit unit, double? total = null);
}

/// <summary>One running progress bar.</summary>
public interface IProgressTask : IDisposable
{
    void Report(double completed, double? total = null);
}
