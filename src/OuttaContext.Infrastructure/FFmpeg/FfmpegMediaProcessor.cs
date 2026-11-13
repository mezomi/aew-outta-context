using System.Globalization;
using System.Text;
using System.Text.Json;
using OuttaContext.Core.Media;
using OuttaContext.Core.Results;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.FFmpeg;

/// <summary>
/// The only class that runs ffmpeg/ffprobe. Each method builds a command, runs it, and returns a
/// <see cref="ToolResult{T}"/>: success, or what went wrong with ffmpeg's own last error lines.
/// </summary>
public sealed class FfmpegMediaProcessor(IProcessRunner runner, ToolLocator tools, Action<string> detail) : IMediaProcessor
{
    public const string Tool = "ffmpeg";

    /// <summary>A cut is stopped if ffmpeg reports no progress for this long.</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public bool IsAvailable => tools.Find("ffmpeg") is not null && tools.Find("ffprobe") is not null;

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken)
    {
        if (tools.Find("ffmpeg") is not { } ffmpeg)
        {
            return null;
        }

        ProcessResult result = await runner.RunAsync(new ProcessSpec(ffmpeg, ["-version"]), cancellationToken);
        return result.Output.Split('\n').FirstOrDefault()?.Trim();
    }

    public async Task<ToolResult<ProbeResult>> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        if (tools.Find("ffprobe") is not { } ffprobe)
        {
            return Missing();
        }

        ProcessResult result = await RunAsync(new ProcessSpec(ffprobe, ProbeArguments(path)), cancellationToken);
        return result.Succeeded ? ParseProbe(result.Output) : Failed("ffprobe", result);
    }

    public async Task<ToolResult<Done>> NormalizeAsync(
        string source, string destination, NormalizeTarget target, CancellationToken cancellationToken)
    {
        if (tools.Find("ffmpeg") is not { } ffmpeg)
        {
            return Missing();
        }

        ToolResult<ProbeResult> probe = await ProbeAsync(source, cancellationToken);
        if (probe.Failed)
        {
            return probe.Error;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = TemporaryPath(destination);
        ProcessResult result = await RunAsync(
            new ProcessSpec(ffmpeg, NormalizeArguments(source, temporary, target, probe.Value.HasAudio)), cancellationToken);
        return Finish(result, temporary, destination);
    }

    public async Task<ToolResult<Done>> ConcatAsync(
        IReadOnlyList<string> paths, string destination, string workDirectory, CancellationToken cancellationToken)
    {
        if (tools.Find("ffmpeg") is not { } ffmpeg)
        {
            return Missing();
        }

        Directory.CreateDirectory(workDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string listFile = Path.Combine(workDirectory, "concat.txt");
        await File.WriteAllTextAsync(listFile, ConcatList(paths), cancellationToken);
        string temporary = TemporaryPath(destination);
        ProcessResult result = await RunAsync(new ProcessSpec(ffmpeg, ConcatArguments(listFile, temporary)), cancellationToken);
        return Finish(result, temporary, destination);
    }

    public async Task<ToolResult<Done>> CutAsync(
        IReadOnlyList<CutInput> inputs,
        string destination,
        double length,
        Action<double> onProgressSeconds,
        CancellationToken cancellationToken)
    {
        if (tools.Find("ffmpeg") is not { } ffmpeg)
        {
            return Missing();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        string temporary = TemporaryPath(destination);
        var spec = new ProcessSpec(ffmpeg, CutArguments(inputs, temporary, length))
        {
            StallTimeout = StallTimeout,
            OnOutputLine = line =>
            {
                if (FfmpegProgress.ParseOutTime(line) is { } seconds)
                {
                    onProgressSeconds(Math.Min(seconds, length));
                }
            },
        };
        ProcessResult result = await RunAsync(spec, cancellationToken);
        if (result.Succeeded)
        {
            onProgressSeconds(length);
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
    public static IReadOnlyList<string> CutArguments(IReadOnlyList<CutInput> inputs, string destination, double length)
    {
        var arguments = new List<string> { "-hide_banner", "-nostats", "-loglevel", "error", "-y" };
        foreach (CutInput input in inputs)
        {
            if (input.IsRemote)
            {
                arguments.AddRange(["-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5"]);
                if (input.Headers is { Count: > 0 } headers)
                {
                    arguments.AddRange(["-headers", string.Concat(headers.Select(h => $"{h.Key}: {h.Value}\r\n"))]);
                }
            }

            arguments.AddRange(["-ss", Invariant($"{Math.Max(input.Offset, 0):0.000}"), "-i", input.Source]);
        }

        arguments.AddRange(inputs.Count > 1 ? ["-map", "0:v:0", "-map", "1:a:0"] : ["-map", "0:v:0", "-map", "0:a:0?"]);
        arguments.AddRange(["-t", Invariant($"{length:0.000}")]);
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
        detail(DescribeCommand(spec.FileName, spec.Arguments));
        return runner.RunAsync(spec, cancellationToken);
    }

    private ToolResult<Done> Finish(ProcessResult result, string temporary, string destination)
    {
        if (!result.Succeeded)
        {
            File.Delete(temporary);
            return Failed(Tool, result);
        }

        File.Move(temporary, destination, overwrite: true);
        return ToolResult.Success();
    }

    private static string TemporaryPath(string destination) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, Path.GetFileNameWithoutExtension(destination) + ".part.mp4");

    private static ToolError Missing() =>
        new(Tool, "ffmpeg/ffprobe was not found.", "Install it: `winget install ffmpeg`, then open a new terminal.");

    private ToolError Failed(string program, ProcessResult result) => result switch
    {
        { NotFound: true } => Missing(),
        { Stalled: true } => new ToolError(
            Tool,
            $"{program} made no progress for {StallTimeout.TotalSeconds:0}s and was stopped. {result.ErrorTail(3)}".Trim(),
            "The site may be refusing the stream. Rerun with -v to see details.",
            result.Error),
        _ => new ToolError(
            Tool,
            $"{program} failed (exit {result.ExitCode}): {result.ErrorTail()}".Trim(),
            "Rerun with -v to see the command and its output.",
            result.Error),
    };

    private static ToolResult<ProbeResult> ParseProbe(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json.Length == 0 ? "{}" : json);
        JsonElement root = document.RootElement;
        double duration = root.TryGetProperty("format", out JsonElement format)
            && format.TryGetProperty("duration", out JsonElement value)
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : 0;
        List<JsonElement> streams = root.TryGetProperty("streams", out JsonElement list) ? [.. list.EnumerateArray()] : [];
        JsonElement? video = streams.FirstOrDefault(s => Kind(s) == "video") is { ValueKind: JsonValueKind.Object } found ? found : null;
        return ToolResult<ProbeResult>.Success(new ProbeResult(
            duration,
            video is { } v && v.TryGetProperty("width", out JsonElement w) ? w.GetInt32() : null,
            video is { } v2 && v2.TryGetProperty("height", out JsonElement h) ? h.GetInt32() : null,
            video is { } v3 && v3.TryGetProperty("avg_frame_rate", out JsonElement r) ? FfmpegProgress.ParseRate(r.GetString()) : null,
            streams.Any(s => Kind(s) == "audio")));

        static string? Kind(JsonElement stream) =>
            stream.TryGetProperty("codec_type", out JsonElement type) ? type.GetString() : null;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
