using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Time;

/// <summary>A part of a video, from <see cref="Start"/> to <see cref="End"/> seconds.</summary>
public readonly record struct TimeRange(double Start, double End)
{
    /// <summary>How far past the video's end a range may go (rounding in site metadata).</summary>
    private const double EndTolerance = 0.5;

    public double Length => End - Start;

    /// <summary>Parses <c>START-END</c>: <c>90-112</c>, <c>1:30-1:52</c>, <c>1:02:10-1:02:30</c>.</summary>
    public static TimeRange Parse(string text)
    {
        string raw = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        string[] parts = raw.Split('-');
        if (parts.Length != 2)
        {
            throw new InvalidTimeRangeException($"\"{text}\" is not a range. Use START-END, e.g. 1:30-1:52.");
        }

        double start = TimeText.ParseSeconds(parts[0]);
        double end = TimeText.ParseSeconds(parts[1]);
        if (end <= start)
        {
            throw new InvalidTimeRangeException(
                $"\"{text}\": the end ({TimeText.Format(end)}) must be after the start ({TimeText.Format(start)}).");
        }

        return new TimeRange(start, end);
    }

    /// <summary>Throws if the range doesn't fit in a video of <paramref name="duration"/> seconds.</summary>
    public void EnsureWithin(double? duration)
    {
        if (duration is not { } length)
        {
            return;
        }

        if (Start >= length)
        {
            throw new RangeOutsideVideoException(
                $"Start {TimeText.Format(Start)} is past the end of the video ({TimeText.Format(length)}).");
        }

        if (End > length + EndTolerance)
        {
            throw new RangeOutsideVideoException(
                $"End {TimeText.Format(End)} is past the end of the video ({TimeText.Format(length)}).");
        }
    }

    public override string ToString() => $"{TimeText.Format(Start)}-{TimeText.Format(End)}";
}
