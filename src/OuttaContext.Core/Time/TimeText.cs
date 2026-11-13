using System.Globalization;
using System.Text.RegularExpressions;
using OuttaContext.Core.Errors;

namespace OuttaContext.Core.Time;

/// <summary>Reads and writes times like <c>90</c>, <c>1:30</c> and <c>1:02:10</c>.</summary>
public static partial class TimeText
{
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex WholeNumberRegex();

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex SecondsRegex();

    /// <summary>Seconds from <c>90</c>, <c>1:30</c>, <c>1:02:10</c> (decimal seconds allowed).</summary>
    public static double ParseSeconds(string text)
    {
        string raw = text.Trim();
        if (raw.Length == 0)
        {
            throw new InvalidTimeRangeException("Empty time value.");
        }

        string[] parts = raw.Split(':');
        bool wellFormed = parts.Length <= 3
            && parts.All(p => p.Length > 0)
            && parts[..^1].All(p => WholeNumberRegex().IsMatch(p))
            && SecondsRegex().IsMatch(parts[^1]);
        if (!wellFormed)
        {
            throw new InvalidTimeRangeException($"\"{text}\" is not a time. Use seconds, M:SS or H:MM:SS.");
        }

        double seconds = double.Parse(parts[^1], CultureInfo.InvariantCulture);
        if (parts.Length >= 2 && seconds >= 60)
        {
            throw new InvalidTimeRangeException($"\"{text}\": seconds must be under 60 in M:SS form.");
        }

        int minutes = parts.Length >= 2 ? int.Parse(parts[^2], CultureInfo.InvariantCulture) : 0;
        if (parts.Length == 3 && minutes > 59)
        {
            throw new InvalidTimeRangeException($"\"{text}\": minutes must be under 60 in H:MM:SS form.");
        }

        int hours = parts.Length == 3 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        return (hours * 3600) + (minutes * 60) + seconds;
    }

    /// <summary>M:SS or H:MM:SS, e.g. <c>1:05</c>, <c>1:02:10</c>.</summary>
    public static string Format(double seconds)
    {
        long total = seconds < 0 ? 0 : (long)Math.Round(seconds, MidpointRounding.ToEven);
        long hours = total / 3600;
        long minutes = total % 3600 / 60;
        long secs = total % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{secs:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{secs:00}");
    }

    /// <summary>Short length for status lines: <c>20s</c> under a minute, else M:SS.</summary>
    public static string FormatLength(double seconds) =>
        seconds < 60
            ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(seconds, MidpointRounding.ToEven)}s")
            : Format(seconds);
}
