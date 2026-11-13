using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OuttaContext.Core.Results;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Uses yt-dlp as a downloader for exact URLs. It writes a small "info JSON" describing the
/// pieces to fetch and runs <c>yt-dlp --load-info-json job.json --impersonate chrome</c>.
/// yt-dlp then downloads every piece with browser impersonation and joins them into one file.
/// If posing as Chrome isn't possible or is refused, it tries Firefox, then a plain request.
/// </summary>
public sealed partial class YtDlpStreamFetcher(
    IProcessRunner runner, ToolLocator tools, YtDlpOptions options, Action<string> detail) : IStreamFetcher
{
    /// <summary>How long a download may show no progress before it is stopped.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(90);

    private const string ProgressMarker = "OUTTA-PIECE";

    private string? _workingTarget;
    private bool _foundWorkingTarget;

    public async Task<ToolResult<string>> DownloadTextAsync(
        string url, IReadOnlyDictionary<string, string> headers, string workFile, CancellationToken cancellationToken)
    {
        var job = new Dictionary<string, object>(JobBase(url, headers))
        {
            ["protocol"] = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "http" : "https",
            ["url"] = url,
            ["ext"] = "m3u8",
        };
        ToolResult<Done> download = await RunJobAsync(job, workFile, _ => { }, cancellationToken);
        return download.Succeeded
            ? ToolResult<string>.Success(await File.ReadAllTextAsync(workFile, cancellationToken))
            : download.Error;
    }

    public Task<ToolResult<Done>> DownloadPiecesAsync(
        IReadOnlyList<StreamPiece> pieces,
        IReadOnlyDictionary<string, string> headers,
        string outputPath,
        Action<int> onPieceDone,
        CancellationToken cancellationToken)
    {
        var fragments = pieces.Select(piece =>
        {
            var fragment = new Dictionary<string, object> { ["url"] = piece.Url };
            if (piece.Duration is { } duration)
            {
                fragment["duration"] = duration;
            }

            if (piece.Range is { } range)
            {
                // yt-dlp's byte_range end is exclusive.
                fragment["byte_range"] = new Dictionary<string, long> { ["start"] = range.First, ["end"] = range.Last + 1 };
            }

            return fragment;
        }).ToList();

        var job = new Dictionary<string, object>(JobBase(pieces[0].Url, headers))
        {
            ["protocol"] = "http_dash_segments",
            ["url"] = pieces[0].Url,
            ["fragments"] = fragments,
            ["ext"] = "mp4",
        };
        return RunJobAsync(job, outputPath, onPieceDone, cancellationToken);
    }

    private static Dictionary<string, object> JobBase(string url, IReadOnlyDictionary<string, string> headers) => new()
    {
        ["id"] = "outta-download",
        ["title"] = "outta-download",
        ["extractor"] = "generic",
        ["extractor_key"] = "Generic",
        ["webpage_url"] = url,
        ["format_id"] = "outta",
        ["http_headers"] = headers,
    };

    private async Task<ToolResult<Done>> RunJobAsync(
        Dictionary<string, object> job, string outputPath, Action<int> onPieceDone, CancellationToken cancellationToken)
    {
        if (tools.Find("yt-dlp") is not { } ytDlp)
        {
            return YtDlpVideoResolver.YtDlpMissing();
        }

        string jobFile = outputPath + ".job.json";
        await File.WriteAllTextAsync(jobFile, JsonSerializer.Serialize(job), cancellationToken);
        var failures = new List<string>();
        try
        {
            foreach (string? target in OrderedTargets())
            {
                ProcessResult result = await runner.RunAsync(
                    new ProcessSpec(ytDlp, Arguments(jobFile, outputPath, target))
                    {
                        StallTimeout = StallTimeout,
                        OnOutputLine = line => ReportPiece(line, onPieceDone),
                        OnErrorLine = detail,
                    },
                    cancellationToken);

                if (result.Succeeded && File.Exists(outputPath))
                {
                    if (!_foundWorkingTarget)
                    {
                        detail($"Stream requests work as: {target ?? "plain HTTP"}");
                    }

                    (_workingTarget, _foundWorkingTarget) = (target, true);
                    return ToolResult.Success();
                }

                string reason = result.Stalled ? $"no progress for {StallTimeout.TotalSeconds:0}s" : YtDlpErrors.Clean(result.Error);
                failures.Add($"{target ?? "plain"}: {reason}");
                detail($"Stream request as {target ?? "plain HTTP"} failed: {reason}");
            }
        }
        finally
        {
            File.Delete(jobFile);
        }

        return new ToolError(
            YtDlpErrors.Tool,
            "The site refused the stream: " + string.Join("; ", failures),
            "Try `--cookies-from-browser firefox`, or copy the .m3u8 link from your browser.");
    }

    private IEnumerable<string?> OrderedTargets() =>
        _foundWorkingTarget
            ? new[] { _workingTarget }.Concat(options.ImpersonateTargets.Where(target => target != _workingTarget))
            : options.ImpersonateTargets;

    private List<string> Arguments(string jobFile, string outputPath, string? target)
    {
        var arguments = new List<string>(options.CommonArguments())
        {
            "--load-info-json", jobFile,
            "--output", outputPath,
            "--force-overwrites", "--no-part", "--fixup", "never", "--no-cache-dir",
            "--newline", "--progress-template", $"download:{ProgressMarker} %(progress.fragment_index)s",
        };
        if (target is not null)
        {
            arguments.Add("--impersonate");
            arguments.Add(target);
        }

        return arguments;
    }

    private static void ReportPiece(string line, Action<int> onPieceDone)
    {
        Match match = PieceProgressRegex().Match(line);
        if (match.Success)
        {
            onPieceDone(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
        }
    }

    [GeneratedRegex(ProgressMarker + @" (\d+)")]
    private static partial Regex PieceProgressRegex();
}
