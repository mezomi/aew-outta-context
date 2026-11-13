using System.Globalization;

namespace OuttaContext.Infrastructure.FFmpeg;

/// <summary>Reads ffmpeg's <c>-progress</c> output and ffprobe frame rates.</summary>
public static class FfmpegProgress
{
    /// <summary>Seconds of output written, from a line like <c>out_time_us=2000000</c>; null for other lines.</summary>
    public static double? ParseOutTime(string line)
    {
        string[] parts = line.Trim().Split('=', 2);
        // ffmpeg prints out_time_ms in microseconds too, despite the name.
        return parts.Length == 2 && parts[0] is "out_time_us" or "out_time_ms"
            && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds)
                ? microseconds / 1_000_000.0
                : null;
    }

    /// <summary>Frames per second from ffprobe's "30000/1001" style.</summary>
    public static double? ParseRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate) || rate == "0/0")
        {
            return null;
        }

        string[] parts = rate.Split('/');
        if (parts.Length == 1)
        {
            return double.Parse(parts[0], CultureInfo.InvariantCulture);
        }

        double denominator = double.Parse(parts[1], CultureInfo.InvariantCulture);
        return denominator == 0 ? null : double.Parse(parts[0], CultureInfo.InvariantCulture) / denominator;
    }
}
