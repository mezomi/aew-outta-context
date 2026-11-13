using System.Globalization;
using System.Text;
using System.Text.Json;
using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.FFmpeg;

/// <summary>
/// The only class that runs ffmpeg/ffprobe. Each method builds a command, runs it, and returns a
/// <c>Result</c>: success, or what went wrong with ffmpeg's own last error lines.
/// </summary>
public sealed class FfmpegMediaProcessor(IProcessRunner runner, ToolLocator tools, IReporter reporter) : IMediaProcessor
{
    /// <summary>A cut is stopped if ffmpeg reports no progress for this long.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public bool IsAvailable => tools.Find("ffmpeg").HasValue && tools.Find("ffprobe").HasValue;

    public async Task<Maybe<string>> GetVersionAsync(CancellationToken cancellationToken)
    {
        if (!tools.Find("ffmpeg").TryGetValue(out string? ffmpeg))
        {
            return Maybe<string>.None;
        }

        ProcessResult result = await runner.RunAsync(new ProcessSpec(ffmpeg, ["-version"]), cancellationToken);
        string firstLine = result.Output.Split('\n')[0].Trim();
        return result.Succeeded && firstLine.Length > 0 ? firstLine : Maybe<string>.None;
    }

    public async Task<Result<ProbeResult, Error>> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        if (!tools.Find("ffprobe").TryGetValue(out string? ffprobe))
        {
            return FfmpegErrors.Missing();
        }

        ProcessResult result = await RunAsync(new ProcessSpec(ffprobe, ProbeArguments(path)), cancellationToken);
        return result.Succeeded ? ParseProbe(result.Output) : FfmpegErrors.Failed("ffprobe", result, StallTimeout);
    }

    public async Task<UnitResult<Error>> NormalizeAsync(
        string source, string destination, NormalizeTarget target, CancellationToken cancellationToken)
    {
        if (!tools.Find("ffmpeg").TryGetValue(out string? ffmpeg))
        {
            return FfmpegErrors.Missing();
        }

        Result<ProbeResult, Error> probe = await ProbeAsync(source, cancellationToken);
        if (probe.IsFailure)
        {
            return probe.Error;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = TemporaryPath(destination);
        ProcessResult result = await RunAsync(
            new ProcessSpec(ffmpeg, NormalizeArguments(source, temporary, target, probe.Value.HasAudio)), cancellationToken);
        return Finish(result, temporary, destination);
    }

    public async Task<UnitResult<Error>> ConcatAsync(
        IReadOnlyList<string> paths, string destination, string workDirectory, CancellationToken cancellationToken)
    {
        if (!tools.Find("ffmpeg").TryGetValue(out string? ffmpeg))
        {
            return FfmpegErrors.Missing();
        }

        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string listFile = Path.Combine(workDirectory, "concat.txt");
        await File.WriteAllTextAsync(listFile, ConcatList(paths), cancellationToken);
        string temporary = TemporaryPath(destination);
        ProcessResult result = await RunAsync(new ProcessSpec(ffmpeg, ConcatArguments(listFile, temporary)), cancellationToken);
        return Finish(result, temporary, destination);
    }

    public async Task<UnitResult<Error>> CutAsync(
        IReadOnlyList<CutInput> inputs,
        string destination,
        VideoTime length,
        Action<double> onProgressSeconds,
        CancellationToken cancellationToken)
    {
        if (!tools.Find("ffmpeg").TryGetValue(out string? ffmpeg))
        {
            return FfmpegErrors.Missing();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = TemporaryPath(destination);
        var spec = new ProcessSpec(ffmpeg, CutArguments(inputs, temporary, length))
        {
            StallTimeout = StallTimeout,
            OnOutputLine = line => FfmpegProgress.ParseOutTime(line).Tap(seconds => onProgressSeconds(Math.Min(seconds, length.Seconds))),
        };
        ProcessResult result = await RunAsync(spec, cancellationToken);
        if (result.Succeeded)
        {
            onProgressSeconds(length.Seconds);
        }

        return Finish(result, temporary, destination);
    }

    // --- Commands (public so tests can check them without running ffmpeg) ---------------

    public static IReadOnlyList<string> ProbeArguments(string path) =>
    [
        "-v", "error", "-show_entries", "format=duration:stream=codec_type,width,height,avg_frame_rate",
        "-of", "json", path,
    ];

    public static IReadOnlyList<string> NormalizeArguments(string source, string destination, NormalizeTarget target, bool hasAudio)
    {
        string filter = string.Create(
            CultureInfo.InvariantCulture,
            $"scale={target.Width}:{target.Height}:force_original_aspect_ratio=decrease,") +
            string.Create(CultureInfo.InvariantCulture, $"pad={target.Width}:{target.Height}:(ow-iw)/2:(oh-ih)/2:color=black,") +
            string.Create(CultureInfo.InvariantCulture, $"setsar=1,fps={target.Fps},format=yuv420p");
        var arguments = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", source };
        if (!hasAudio)
        {
            // A silent track, so every clip has audio and they join cleanly.
            arguments.AddRange(["-f", "lavfi", "-i", Invariant($"anullsrc=r={target.AudioRate}:cl=stereo")]);
        }

        arguments.AddRange(["-map", "0:v:0", "-map", hasAudio ? "0:a:0" : "1:a:0"]);
        arguments.AddRange(["-vf", filter, "-c:v", target.VideoCodec, "-preset", target.Preset, "-crf", Invariant($"{target.Crf}")]);
        arguments.AddRange(["-c:a", target.AudioCodec, "-ar", Invariant($"{target.AudioRate}"), "-ac", "2", "-b:a", "192k"]);
        if (!hasAudio)
        {
            arguments.Add("-shortest");
        }

        arguments.AddRange(["-movflags", "+faststart", destination]);
        return arguments;
    }

    /// <summary>The list file the concat demuxer reads; single quotes in paths are escaped.</summary>
    public static string ConcatList(IEnumerable<string> paths)
    {
        var builder = new StringBuilder();
        foreach (string path in paths)
        {
            string escaped = Path.GetFullPath(path).Replace('\\', '/').Replace("'", @"'\''", StringComparison.Ordinal);
            builder.Append("file '").Append(escaped).Append("'\n");
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> ConcatArguments(string listFile, string destination) =>
    [
        "-hide_banner", "-loglevel", "error", "-y", "-f", "concat", "-safe", "0", "-i", listFile,
        "-c", "copy", "-movflags", "+faststart", destination,
    ];

    /// <summary>Exact cut: seek each input, re-encode <paramref name="length"/> seconds, report progress on stdout.</summary>
    public static IReadOnlyList<string> CutArguments(IReadOnlyList<CutInput> inputs, string destination, VideoTime length)
    {
        var arguments = new List<string> { "-hide_banner", "-nostats", "-loglevel", "error", "-y" };
        foreach (CutInput input in inputs)
        {
            if (input.IsRemote)
            {
                arguments.AddRange(["-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5"]);
                if (!input.Headers.IsEmpty)
                {
                    arguments.AddRange(["-headers", string.Concat(input.Headers.Values.Select(h => $"{h.Key}: {h.Value}\r\n"))]);
                }
            }

            arguments.AddRange(["-ss", Invariant($"{input.Offset.Seconds:0.000}"), "-i", input.Source]);
        }

        arguments.AddRange(inputs.Count > 1 ? ["-map", "0:v:0", "-map", "1:a:0"] : ["-map", "0:v:0", "-map", "0:a:0?"]);
        arguments.AddRange(["-t", Invariant($"{length.Seconds:0.000}")]);
        arguments.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p"]);
        arguments.AddRange(["-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"]);
        arguments.AddRange(["-progress", "pipe:1", destination]);
        return arguments;
    }

    /// <summary>A command for the --verbose log, with header values (which can hold cookies) hidden.</summary>
    public static string DescribeCommand(string program, IReadOnlyList<string> arguments)
    {
        var shown = arguments.ToList();
        for (int index = 0; index < shown.Count - 1; index++)
        {
            if (shown[index] == "-headers")
            {
                shown[index + 1] = "<headers>";
            }
        }

        return "$ " + Path.GetFileName(program) + " " + string.Join(' ', shown.Select(a => a.Contains(' ', StringComparison.Ordinal) ? $"\"{a}\"" : a));
    }

    // --- Helpers -------------------------------------------------------------------------

    private Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        reporter.Detail(DescribeCommand(spec.FileName, spec.Arguments));
        return runner.RunAsync(spec, cancellationToken);
    }

    private UnitResult<Error> Finish(ProcessResult result, string temporary, string destination)
    {
        if (!result.Succeeded)
        {
            File.Delete(temporary);
            return FfmpegErrors.Failed("ffmpeg", result, StallTimeout);
        }

        File.Move(temporary, destination, overwrite: true);
        return UnitResult.Success<Error>();
    }

    private static string TemporaryPath(string destination) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, Path.GetFileNameWithoutExtension(destination) + ".part.mp4");

    private static Result<ProbeResult, Error> ParseProbe(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json.Length == 0 ? "{}" : json);
            JsonElement root = document.RootElement;
            double duration = root.TryGetProperty("format", out JsonElement format)
                && format.TryGetProperty("duration", out JsonElement value)
                && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                && parsed >= 0
                    ? parsed
                    : 0;
            List<JsonElement> streams = root.TryGetProperty("streams", out JsonElement list) ? [.. list.EnumerateArray()] : [];
            Maybe<JsonElement> video = streams.TryFirst(stream => Kind(stream) == "video");
            Maybe<int> width = video.Bind(v => Integer(v, "width"));
            Maybe<int> height = video.Bind(v => Integer(v, "height"));
            Maybe<double> fps = video.Bind(v => v.TryGetProperty("avg_frame_rate", out JsonElement rate) ? FfmpegProgress.ParseRate(rate.GetString()) : Maybe<double>.None);
            return new ProbeResult(
                VideoTime.FromTrusted(duration),
                width.AsNullable(),
                height.AsNullable(),
                fps.AsNullable(),
                streams.Any(stream => Kind(stream) == "audio"));
        }
        catch (JsonException exception)
        {
            return FfmpegErrors.UnreadableProbe(exception.Message);
        }

        static string? Kind(JsonElement stream) =>
            stream.TryGetProperty("codec_type", out JsonElement type) ? type.GetString() : null;

        static Maybe<int> Integer(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int number) ? number : Maybe<int>.None;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
