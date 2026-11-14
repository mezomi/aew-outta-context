namespace OuttaContext.Core.Clips;

/// <summary>
/// Whether `outta clip` replaces an existing clip (<c>--replace [ID]</c>): none, the clip that already
/// has the title, or a clip by ID. A replaced clip keeps its ID and its place in its episode.
/// </summary>
public sealed record ClipReplacement
{
    /// <summary>Make a new clip; a title that's taken is refused.</summary>
    public static readonly ClipReplacement None = new(false, Maybe<long>.None);

    /// <summary><c>--replace</c>: replace the clip that has this title.</summary>
    public static readonly ClipReplacement SameTitle = new(true, Maybe<long>.None);

    private ClipReplacement(bool isRequested, Maybe<long> clipId)
    {
        IsRequested = isRequested;
        ClipId = clipId;
    }

    public bool IsRequested { get; }

    /// <summary>Set with <c>--replace ID</c>.</summary>
    public Maybe<long> ClipId { get; }

    /// <summary><c>--replace ID</c>: replace this clip (it may get a new title).</summary>
    public static ClipReplacement OfClip(long clipId) => new(true, clipId);
}
