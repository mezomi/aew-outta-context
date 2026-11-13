using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Composition;

/// <summary>
/// Handles exceptions that reach the top. Expected failures don't come here: commands return them
/// as results (see <c>CommandResults</c>). What does come here is usage errors, Ctrl+C,
/// <see cref="ErrorException"/> (e.g. a corrupt data file) and bugs.
/// </summary>
public static class ErrorHandler
{
    public static int Handle(Exception exception, IAnsiConsole console, SpectreReporter? reporter)
    {
        var printer = reporter ?? new SpectreReporter(console);

        // Spectre wraps exceptions thrown while creating a command (e.g. a broken settings.toml
        // found when OuttaSettings is first needed), so look inside for ours.
        if (InnerError(exception) is { } inner)
        {
            exception = inner;
        }

        switch (exception)
        {
            case ErrorException error:
                printer.Error(error.Error);
                ShowTrace(exception, console, printer);
                return ExitCodes.Failure;

            case CommandAppException usage:
                // Wrong options or arguments: Spectre explains which.
                if (usage.Pretty is { } pretty)
                {
                    console.Write(pretty);
                }
                else
                {
                    printer.Error(new Error("usage", usage.Message, "Run the command with --help to see its options."));
                }

                return ExitCodes.UsageError;

            case OperationCanceledException:
                console.MarkupLine("[yellow]Cancelled.[/]");
                return ExitCodes.Cancelled;

            default:
                // A bug: say so plainly; the full trace is shown with --verbose.
                printer.Error(new Error(
                    "unexpected",
                    $"Unexpected problem: {exception.GetType().Name}: {exception.Message}",
                    "Rerun with --verbose for details."));
                ShowTrace(exception, console, printer);
                return ExitCodes.Failure;
        }
    }

    private static ErrorException? InnerError(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is ErrorException error)
            {
                return error;
            }
        }

        return null;
    }

    private static void ShowTrace(Exception exception, IAnsiConsole console, SpectreReporter reporter)
    {
        if (reporter.IsVerbose)
        {
            console.WriteException(exception, ExceptionFormats.ShortenEverything);
        }
    }
}
