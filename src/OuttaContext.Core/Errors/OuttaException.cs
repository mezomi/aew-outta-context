namespace OuttaContext.Core.Errors;

/// <summary>
/// A problem the user can fix: the message says what went wrong, the hint says how to fix it.
/// </summary>
/// <remarks>
/// Throw this (or a subclass) when one of our own rules is broken, for example a clip range that is
/// longer than the cap. The CLI catches it in one place and prints <c>error:</c> and <c>hint:</c>.
/// Failures of outside programs (yt-dlp, ffmpeg, rclone) are returned as
/// <see cref="Results.ToolResult{T}"/> instead; see that class.
/// </remarks>
public class OuttaException : Exception
{
    public OuttaException(string message, string? hint = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Hint = hint ?? string.Empty;
    }

    public OuttaException()
        : this("Something went wrong.")
    {
    }

    public OuttaException(string message)
        : this(message, null, null)
    {
    }

    public OuttaException(string message, Exception innerException)
        : this(message, null, innerException)
    {
    }

    /// <summary>How the user can fix the problem. Empty when there is nothing to suggest.</summary>
    public string Hint { get; }
}
