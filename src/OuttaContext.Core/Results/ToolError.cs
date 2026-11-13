using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Results;

/// <summary>
/// Why an outside program (yt-dlp, ffmpeg, rclone, a website) did not do what we asked.
/// </summary>
/// <param name="Tool">Which program failed, e.g. "yt-dlp".</param>
/// <param name="Message">What went wrong, in plain words.</param>
/// <param name="Hint">How the user can fix it.</param>
/// <param name="Details">The program's own output (shown with --verbose).</param>
public sealed record ToolError(string Tool, string Message, string Hint, string? Details = null)
{
    /// <summary>Turn the error into an exception the CLI prints as <c>error:</c> / <c>hint:</c>.</summary>
    public OuttaException ToException() => new ToolFailedException(this);
}

/// <summary>Thrown by <see cref="ToolResult{T}.ValueOrThrow"/> when we give up on a failed tool.</summary>
#pragma warning disable CA1032 // Always built from a ToolError.
public sealed class ToolFailedException(ToolError error) : OuttaException(error.Message, error.Hint)
#pragma warning restore CA1032
{
    public ToolError Error { get; } = error;
}
