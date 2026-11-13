using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Common;

namespace OuttaContext.Core.Time;

/// <summary>
/// A point in a video, or a length of time, in seconds. Never negative.
/// Read with <see cref="Parse"/> (<c>90</c>, <c>1:30</c>, <c>1:02:10</c>), shown as M:SS or H:MM:SS.
/// </summary>
public sealed partial class VideoTime : ComparableValueObject
{
    public static readonly VideoTime Zero = new(0);

    private VideoTime(double seconds)
    {
        Seconds = seconds;
    }

    public double Seconds { get; }

    public static Result<VideoTime, Error> FromSeconds(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0 ? new VideoTime(seconds) : TimeErrors.Negative(seconds);

    /// <summary>For times the app computes itself (sums, playlist positions); a negative one is a bug, so this throws.</summary>
    public static VideoTime FromTrusted(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0
            ? new VideoTime(seconds)
            : throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "A video time can't be negative.");

    /// <summary>Reads <c>90</c>, <c>1:30</c> or <c>1:02:10</c> (decimal seconds allowed).</summary>
    public static Result<VideoTime, Error> Parse(string text)
    {
        string raw = text.Trim();
        string[] parts = raw.Split(':');
        bool wellFormed = raw.Length > 0
            && parts.Length <= 3
            && parts.All(part => part.Length > 0)
            && parts[..^1].All(part => WholeNumberRegex().IsMatch(part))
            && SecondsRegex().IsMatch(parts[^1]);
        if (!wellFormed)
        {
            return TimeErrors.NotATime(text);
        }

        double seconds = double.Parse(parts[^1], CultureInfo.InvariantCulture);
        if (parts.Length >= 2 && seconds >= 60)
        {
            return TimeErrors.SecondsTooLarge(text);
        }

        int minutes = parts.Length >= 2 ? int.Parse(parts[^2], CultureInfo.InvariantCulture) : 0;
        if (parts.Length == 3 && minutes > 59)
        {
            return TimeErrors.MinutesTooLarge(text);
        }

        int hours = parts.Length == 3 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        return new VideoTime((hours * 3600) + (minutes * 60) + seconds);
    }

    public VideoTime Plus(VideoTime other) => new(Seconds + other.Seconds);

    /// <summary>This minus <paramref name="other"/>, or zero if that would be negative.</summary>
    public VideoTime MinusOrZero(VideoTime other) => new(Math.Max(0, Seconds - other.Seconds));

    public bool IsLongerThan(VideoTime other) => Seconds > other.Seconds + 1e-6;

    public bool IsAtLeast(VideoTime other) => Seconds >= other.Seconds;

    /// <summary>M:SS or H:MM:SS, e.g. <c>1:05</c>, <c>1:02:10</c>.</summary>
    public string ToClock()
    {
        long total = (long)Math.Round(Seconds, MidpointRounding.ToEven);
        long hours = total / 3600;
        long minutes = total % 3600 / 60;
        long secs = total % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{secs:00}");
    }

    /// <summary>Short length for status lines: <c>20s</c> under a minute, else M:SS.</summary>
    public string ToShortLength() =>
        Seconds < 60 ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(Seconds, MidpointRounding.ToEven)}s") : ToClock();

    public override string ToString() => ToClock();

    protected override IEnumerable<IComparable> GetComparableEqualityComponents()
    {
        yield return Seconds;
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex WholeNumberRegex();

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex SecondsRegex();
}
