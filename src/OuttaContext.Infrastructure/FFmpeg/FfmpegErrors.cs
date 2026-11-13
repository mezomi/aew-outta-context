using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.FFmpeg;

/// <summary>Every <see cref="Error"/> that comes from ffmpeg or ffprobe.</summary>
public static class FfmpegErrors
{
    public static Error Missing() => MediaErrors.FfmpegMissing();

    public static Error UnreadableProbe(string problem) =>
        new("ffprobe.unreadable", $"ffprobe returned unreadable output: {problem}", "Reinstall ffmpeg: `winget install ffmpeg`.");

    /// <summary>The error for a run of <paramref name="program"/> that did not succeed.</summary>
    public static Error Failed(string program, ProcessResult result, TimeSpan stallTimeout) => result switch
    {
        { NotFound: true } => Missing(),
        { Stalled: true } => new Error(
            $"{program}.stalled",
            $"{program} made no progress for {stallTimeout.TotalSeconds:0}s and was stopped. {result.ErrorTail(3)}".Trim(),
            "The site may be refusing the stream. Rerun with -v to see details.",
            result.Error),
        _ => new Error(
            $"{program}.failed",
            $"{program} failed (exit {result.ExitCode}): {result.ErrorTail()}".Trim(),
            "Rerun with -v to see the command and its output.",
            result.Error),
    };
}
