using OuttaContext.Core.Common;

namespace OuttaContext.Core.Time;

/// <summary>
/// The part of a video a clip covers: from <see cref="Start"/> to <see cref="End"/>, end after start.
/// Read from text like <c>1:30-1:52</c> with <see cref="Parse"/>.
/// </summary>
public sealed class ClipTimeSpan : ValueObject
{
    /// <summary>How far past the video's end a span may go (sites round their lengths).</summary>
    private const double EndTolerance = 0.5;

    private ClipTimeSpan(VideoTime start, VideoTime end)
    {
        Start = start;
        End = end;
    }

    public VideoTime Start { get; }

    public VideoTime End { get; }

    public VideoTime Length => End.MinusOrZero(Start);

    /// <summary>Reads <c>START-END</c>: <c>90-112</c>, <c>1:30-1:52</c>, <c>1:02:10-1:02:30</c>.</summary>
    public static Result<ClipTimeSpan, Error> Parse(string text)
    {
        string[] parts = text.Replace(" ", string.Empty, StringComparison.Ordinal).Split('-');
        if (parts.Length != 2)
        {
            return TimeErrors.NotARange(text);
        }

        return VideoTime.Parse(parts[0])
            .Bind(start => VideoTime.Parse(parts[1]).Map(end => (start, end)))
            .Bind(times => Create(times.start, times.end, text));
    }

    public static Result<ClipTimeSpan, Error> Create(VideoTime start, VideoTime end, string? text = null) =>
        end.IsLongerThan(start)
            ? new ClipTimeSpan(start, end)
            : TimeErrors.EndNotAfterStart(text ?? $"{start}-{end}", start, end);

    /// <summary>Fails if the span doesn't fit in a video of this length. An unknown length always fits.</summary>
    public UnitResult<Error> FitsIn(Maybe<VideoTime> videoLength)
    {
        if (videoLength.HasNoValue)
        {
            return UnitResult.Success<Error>();
        }

        VideoTime length = videoLength.Value;
        if (Start.IsAtLeast(length))
        {
            return TimeErrors.StartPastVideoEnd(Start, length);
        }

        return End.Seconds > length.Seconds + EndTolerance
            ? TimeErrors.EndPastVideoEnd(End, length)
            : UnitResult.Success<Error>();
    }

    public override string ToString() => $"{Start}-{End}";

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
