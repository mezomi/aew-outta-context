using OuttaContext.Cli.Composition;
using OuttaContext.Cli.Console;
using OuttaContext.Core.Common;

namespace OuttaContext.Cli.Commands;

/// <summary>
/// Turns a use case's <c>Result</c> into the command's exit code. On success, runs
/// <c>onSuccess</c> (to print the result) and returns 0; on failure, prints error + hint and returns 1.
/// </summary>
/// <example>
/// <code>
/// Result&lt;EpisodeStatus, Error&gt; status = episodes.Status(settings.Name);
/// return status.ToExitCode(reporter, found => EpisodeView.Write(console, found));
/// </code>
/// </example>
public static class CommandResults
{
    public static int ToExitCode<T>(this Result<T, Error> result, SpectreReporter reporter, Action<T>? onSuccess = null)
    {
        if (result.IsFailure)
        {
            reporter.Error(result.Error);
            return ExitCodes.Failure;
        }

        onSuccess?.Invoke(result.Value);
        return ExitCodes.Success;
    }

    public static int ToExitCode(this UnitResult<Error> result, SpectreReporter reporter, Action? onSuccess = null)
    {
        if (result.IsFailure)
        {
            reporter.Error(result.Error);
            return ExitCodes.Failure;
        }

        onSuccess?.Invoke();
        return ExitCodes.Success;
    }

    public static async Task<int> ToExitCode<T>(this Task<Result<T, Error>> result, SpectreReporter reporter, Action<T>? onSuccess = null) =>
        (await result).ToExitCode(reporter, onSuccess);

    public static async Task<int> ToExitCode(this Task<UnitResult<Error>> result, SpectreReporter reporter, Action? onSuccess = null) =>
        (await result).ToExitCode(reporter, onSuccess);
}
