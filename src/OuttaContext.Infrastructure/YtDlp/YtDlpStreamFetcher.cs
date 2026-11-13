using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OuttaContext.Core.Common;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Sources;
using OuttaContext.Infrastructure.Processes;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Uses yt-dlp as a downloader for exact URLs. It writes a small "info JSON" describing the
/// pieces to fetch and runs <c>yt-dlp --load-info-json job.json --impersonate chrome</c>.
/// yt-dlp then downloads every piece with browser impersonation and joins them into one file.
/// If posing as Chrome isn't possible or is refused, it tries Firefox, then a plain request.
/// </summary>
public sealed partial class YtDlpStreamFetcher(
    IProcessRunner runner, ToolLocator tools, YtDlpOptions options, IReporter reporter) : IStreamFetcher
{
    /// <summary>How long a download may show no progress before it is stopped.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(90);

    private const string ProgressMarker = "OUTTA-PIECE";

    /// <summary>The impersonation target that worked last time, tried first next time.</summary>
    private Maybe<Maybe<string>> _workingTarget;

    public async Task<Result<string, Error>> DownloadTextAsync(
        string url, RequestHeaders headers, string workFile, CancellationToken cancellationToken)
    {
        var job = new Dictionary<string, object>(JobBase(url, headers))
        {
            ["protocol"] = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "http" : "https",
            ["url"] = url,
            ["ext"] = "m3u8",
        };
        UnitResult<Error> download = await RunJobAsync(job, workFile, _ => { }, cancellationToken);
        return download.IsSuccess
            ? await File.ReadAllTextAsync(workFile, cancellationToken)
            : download.Error;
    }

    public Task<UnitResult<Error>> DownloadPiecesAsync(
        IReadOnlyList<StreamPiece> pieces,
        RequestHeaders headers,
        string outputPath,
        Action<int> onPieceDone,
        CancellationToken cancellationToken)
    {
        var fragments = pieces.Select(piece =>
        {
            var fragment = new Dictionary<string, object> { ["url"] = piece.Url };
            piece.Duration.Tap(duration => fragment["duration"] = duration.Seconds);

            // yt-dlp's byte_range end is exclusive.
            piece.Range.Tap(range => fragment["byte_range"] = new Dictionary<string, long> { ["start"] = range.First, ["end"] = range.EndExclusive });

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

    private static Dictionary<string, object> JobBase(string url, RequestHeaders headers) => new()
    {
        ["id"] = "outta-download",
        ["title"] = "outta-download",
        ["extractor"] = "generic",
        ["extractor_key"] = "Generic",
        ["webpage_url"] = url,
        ["format_id"] = "outta",
        ["http_headers"] = headers.Values,
    };

    private async Task<UnitResult<Error>> RunJobAsync(
        Dictionary<string, object> job, string outputPath, Action<int> onPieceDone, CancellationToken cancellationToken)
    {
        if (!tools.Find("yt-dlp").TryGetValue(out string? ytDlp))
        {
            return YtDlpErrors.Missing();
        }

        string jobFile = outputPath + ".job.json";
        await File.WriteAllTextAsync(jobFile, JsonSerializer.Serialize(job), cancellationToken);
        var failures = new List<string>();
        try
        {
            foreach (Maybe<string> target in OrderedTargets())
            {
                ProcessResult result = await runner.RunAsync(
                    new ProcessSpec(ytDlp, Arguments(jobFile, outputPath, target))
                    {
                        StallTimeout = StallTimeout,
                        OnOutputLine = line => ReportPiece(line, onPieceDone),
                        OnErrorLine = reporter.Detail,
                    },
                    cancellationToken);

                if (result.Succeeded && File.Exists(outputPath))
                {
                    if (_workingTarget.HasNoValue)
                    {
                        reporter.Detail($"Stream requests work as: {Describe(target)}");
                    }

                    _workingTarget = target;
                    return UnitResult.Success<Error>();
                }

                string reason = result.Stalled ? $"no progress for {StallTimeout.TotalSeconds:0}s" : YtDlpErrors.Clean(result.Error);
                failures.Add($"{target.GetValueOrDefault("plain")}: {reason}");
                reporter.Detail($"Stream request as {Describe(target)} failed: {reason}");
            }
        }
        finally
        {
            File.Delete(jobFile);
        }

        return YtDlpErrors.StreamRefused(failures);
    }

    private static string Describe(Maybe<string> target) => target.GetValueOrDefault("plain HTTP");

    private IEnumerable<Maybe<string>> OrderedTargets() =>
        _workingTarget.TryGetValue(out Maybe<string> working)
            ? new[] { working }.Concat(options.ImpersonateTargets.Where(target => target != working))
            : options.ImpersonateTargets;

    private List<string> Arguments(string jobFile, string outputPath, Maybe<string> target)
    {
        var arguments = new List<string>(options.CommonArguments())
        {
            "--load-info-json", jobFile,
            "--output", outputPath,
            "--force-overwrites", "--no-part", "--fixup", "never", "--no-cache-dir",
            "--newline", "--progress-template", $"download:{ProgressMarker} %(progress.fragment_index)s",
        };
        if (target.TryGetValue(out string? browser))
        {
            arguments.Add("--impersonate");
            arguments.Add(browser);
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
