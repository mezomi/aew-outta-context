using OuttaContext.Cli.Console;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Composition;

/// <summary>
/// The one place errors become output. Every command lets its errors bubble up to here.
/// </summary>
public static class ErrorHandler
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;
    public const int Cancelled = 130;

    public static int Handle(Exception exception, IAnsiConsole console, SpectreReporter? reporter)
    {
        var printer = reporter ?? new SpectreReporter(console);
        switch (exception)
        {
            case OuttaException outta:
                // A problem the user can fix: message + hint.
                printer.Error(outta.Message, outta.Hint);
                ShowDetails(exception, console, printer);
                return Failure;

            case CommandAppException usage:
                // Wrong options or arguments: Spectre explains which.
                if (usage.Pretty is { } pretty)
                {
                    console.Write(pretty);
                }
                else
                {
                    printer.Error(usage.Message, "Run the command with --help to see its options.");
                }

                return UsageError;

            case OperationCanceledException:
                console.MarkupLine("[yellow]Cancelled.[/]");
                return Cancelled;

            default:
                // A bug: say so plainly; the full trace is shown with --verbose.
                printer.Error($"Unexpected problem: {exception.GetType().Name}: {exception.Message}", "Rerun with --verbose for details.");
                ShowDetails(exception, console, printer);
                return Failure;
        }
    }

    private static void ShowDetails(Exception exception, IAnsiConsole console, SpectreReporter reporter)
    {
        if (reporter.Verbosity == Verbosity.Verbose)
        {
            console.WriteException(exception, ExceptionFormats.ShortenEverything);
        }
    }
}
