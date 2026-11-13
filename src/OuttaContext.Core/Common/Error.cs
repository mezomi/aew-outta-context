namespace OuttaContext.Core.Common;

/// <summary>
/// Why something the user asked for could not be done: a problem we expected might happen
/// (bad input, a site refusing, a missing file), as opposed to a bug.
/// </summary>
/// <remarks>
/// Methods that can fail this way return <c>Result&lt;T, Error&gt;</c> (or <c>UnitResult&lt;Error&gt;</c>)
/// instead of throwing. The CLI prints any <see cref="Error"/> as <c>error: Message</c> / <c>hint: Hint</c>.
/// Each area has a static class that creates its errors, e.g. <c>ClipErrors.TooLong(...)</c>.
/// </remarks>
/// <param name="Code">Stable identifier for tests and logs, e.g. "clip.too_long" or "yt-dlp.network".</param>
/// <param name="Message">What went wrong, in plain words.</param>
/// <param name="Hint">How the user can fix it.</param>
/// <param name="Details">The outside program's own output, shown with --verbose.</param>
public sealed record Error(string Code, string Message, string Hint = "", string? Details = null)
{
    /// <summary>Use when there is no way to continue and the error must become an exception.</summary>
    public ErrorException ToException() => new(this);
}
