using System.Diagnostics;

namespace OuttaContext.IntegrationTests.Support;

/// <summary>Makes test-pattern videos with ffmpeg, and checks which real tools are installed.</summary>
public static class TestVideos
{
    public static bool HasFfmpeg => OnPath("ffmpeg") && OnPath("ffprobe");

    public static bool HasYtDlp => OnPath("yt-dlp");

    public static void RequireFfmpeg() => Assert.SkipUnless(HasFfmpeg, "needs ffmpeg on PATH");

    public static void RequireFfmpegAndYtDlp() => Assert.SkipUnless(HasFfmpeg && HasYtDlp, "needs ffmpeg and yt-dlp on PATH");

    /// <summary>A test pattern with a 440 Hz tone (unless <paramref name="audio"/> is false).</summary>
    public static string Make(string path, double seconds, string size = "1280x720", int rate = 30, bool audio = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var arguments = new List<string> { "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"testsrc2=size={size}:rate={rate}:duration={seconds}" };
        if (audio)
        {
            arguments.AddRange(["-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}:sample_rate=44100"]);
        }

        arguments.AddRange(["-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-g", (rate * 2).ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (audio)
        {
            arguments.AddRange(["-c:a", "aac", "-shortest"]);
        }

        arguments.Add(path);
        Run("ffmpeg", arguments);
        return path;
    }

    /// <summary>Splits a video into an HLS stream of 4-second pieces. "fmp4" is what Dailymotion serves.</summary>
    public static string MakeHls(string source, string folder, string segmentType)
    {
        Directory.CreateDirectory(folder);
        string playlist = Path.Combine(folder, "manifest.m3u8");
        Run("ffmpeg", ["-loglevel", "error", "-y", "-i", source, "-c", "copy", "-f", "hls", "-hls_time", "4",
            "-hls_playlist_type", "vod", "-hls_segment_type", segmentType, playlist]);
        return playlist;
    }

    private static void Run(string program, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(program) { RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(info)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{program} failed: {error}");
        }
    }

    private static bool OnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, OperatingSystem.IsWindows() ? name + ".exe" : name)));
}
