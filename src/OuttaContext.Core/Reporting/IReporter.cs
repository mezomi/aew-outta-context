namespace OuttaContext.Core.Reporting;

/// <summary>
/// How use cases tell the user what is happening. The CLI draws it with Spectre.Console;
/// tests use a recording version and check the messages.
/// </summary>
public interface IReporter
{
    /// <summary>True with --verbose: callers may then ask outside programs for extra output.</summary>
    bool IsVerbose { get; }

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

    IProgressTask StartProgress(string description, ProgressUnit unit, double? total = null);
}
