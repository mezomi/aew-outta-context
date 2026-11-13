using System.Globalization;

namespace OuttaContext.Infrastructure.FFmpeg;

/// <summary>Reads ffmpeg's <c>-progress</c> output and ffprobe frame rates.</summary>
public static class FfmpegProgress
{
    /// <summary>Seconds of output written, from a line like <c>out_time_us=2000000</c>; none for other lines.</summary>
    public static Maybe<double> ParseOutTime(string line)
    {
        string[] parts = line.Trim().Split('=', 2);
        // ffmpeg prints out_time_ms in microseconds too, despite the name.
        return parts.Length == 2 && parts[0] is "out_time_us" or "out_time_ms"
            && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds)
                ? microseconds / 1_000_000.0
                : Maybe<double>.None;
    }

    /// <summary>Frames per second from ffprobe's "30000/1001" style; none for "0/0" or nonsense.</summary>
    public static Maybe<double> ParseRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate) || rate == "0/0")
        {
            return Maybe<double>.None;
        }

        string[] parts = rate.Split('/');
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator))
        {
            return Maybe<double>.None;
        }

        if (parts.Length == 1)
        {
            return numerator;
        }

        return double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator) && denominator != 0
            ? numerator / denominator
            : Maybe<double>.None;
    }
}
