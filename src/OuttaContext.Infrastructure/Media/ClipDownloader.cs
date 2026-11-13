using OuttaContext.Core.Media;
using OuttaContext.Core.Media.Hls;
using OuttaContext.Core.Reporting;
using OuttaContext.Core.Results;
using OuttaContext.Core.Time;
using OuttaContext.Infrastructure.YtDlp;

namespace OuttaContext.Infrastructure.Media;

/// <summary>
/// Downloads only the part of a video a clip needs, then cuts it exactly.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>HLS streams (.m3u8, what Dailymotion uses): read the playlist, fetch only the pieces
/// covering the clip (through yt-dlp, posing as a browser) into local files, then cut locally.</item>
/// <item>Other streams (YouTube): ffmpeg reads the stream directly and cuts.</item>
/// </list>
/// </remarks>
public sealed class ClipDownloader(IStreamFetcher fetcher, IMediaProcessor media) : IClipDownloader
{
    /// <summary>Extra seconds fetched before and after the clip so the cut never runs short.</summary>
    public const double SegmentMargin = 1.0;

    public async Task<ToolResult<DownloadedClip>> DownloadAsync(
        ResolvedVideo video, string outputPath, TimeRange range, IReporter reporter, CancellationToken cancellationToken)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        string workDirectory = Path.Combine(folder, $".outta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        try
        {
            ToolResult<IReadOnlyList<CutInput>> inputs = video.IsHls
                ? await FetchSegmentsAsync(video, range, workDirectory, reporter, cancellationToken)
                : ToolResult<IReadOnlyList<CutInput>>.Success(
                    video.Streams.Select(stream => new CutInput(stream.Url, range.Start, stream.Headers)).ToList());
            if (inputs.Failed)
            {
                return inputs.Error;
            }

            reporter.Step("Cutting the exact range");
            ToolResult<Done> cut;
            using (IProgressTask progress = reporter.StartProgress("cutting", ProgressUnit.Seconds, range.Length))
            {
                cut = await media.CutAsync(
                    inputs.Value, outputPath, range.Length, seconds => progress.Report(seconds, range.Length), cancellationToken);
            }

            return cut.Map(_ => new DownloadedClip(outputPath, new FileInfo(outputPath).Length));
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    private async Task<ToolResult<IReadOnlyList<CutInput>>> FetchSegmentsAsync(
        ResolvedVideo video, TimeRange range, string workDirectory, IReporter reporter, CancellationToken cancellationToken)
    {
        double start = Math.Max(range.Start - SegmentMargin, 0);
        double end = range.End + SegmentMargin;

        // 1. Read each stream's playlist and pick the pieces that cover the clip.
        var plans = new List<(StreamFormat Stream, MediaPlaylist Playlist, IReadOnlyList<HlsSegment> Pieces)>();
        for (int index = 0; index < video.Streams.Count; index++)
        {
            StreamFormat stream = video.Streams[index];
            ToolResult<string> text = await fetcher.DownloadTextAsync(
                stream.Url, stream.Headers, Path.Combine(workDirectory, $"stream{index}.m3u8"), cancellationToken);
            if (text.Failed)
            {
                return text.Error;
            }

            ToolResult<MediaPlaylist> parsed = MediaPlaylistParser.Parse(text.Value, new Uri(stream.Url));
            if (parsed.Failed)
            {
                return parsed.Error;
            }

            MediaPlaylist playlist = parsed.Value;
            if (playlist.Encrypted)
            {
                return new ToolError("HLS", "This stream is encrypted (DRM); it can't be clipped.", "Try another upload of the same footage.");
            }

            IReadOnlyList<HlsSegment> pieces = playlist.Covering(start, end);
            if (pieces.Count == 0)
            {
                return new ToolError(
                    "HLS",
                    $"The stream has no pieces for this range (it is {TimeText.Format(playlist.Duration)} long).",
                    "Check the range.");
            }

            plans.Add((stream, playlist, pieces));
        }

        // 2. Download those pieces (and each stream's header piece) into one local file per stream.
        reporter.Step("Fetching the stream pieces for this range");
        int total = plans.Sum(plan => plan.Pieces.Count);
        int finishedBefore = 0;
        var inputs = new List<CutInput>();
        using (IProgressTask progress = reporter.StartProgress("pieces", ProgressUnit.Segments, total))
        {
            for (int index = 0; index < plans.Count; index++)
            {
                (StreamFormat stream, MediaPlaylist playlist, IReadOnlyList<HlsSegment> pieces) = plans[index];
                var toFetch = new List<StreamPiece>();
                if (playlist.InitUrl is { } initUrl)
                {
                    toFetch.Add(new StreamPiece(initUrl, null, playlist.InitRange));
                }

                toFetch.AddRange(pieces.Select(piece => new StreamPiece(piece.Url, piece.Duration, piece.Range)));
                int headerPieces = toFetch.Count - pieces.Count;
                int offset = finishedBefore;
                string path = Path.Combine(workDirectory, $"stream{index}.mp4");
                ToolResult<Done> download = await fetcher.DownloadPiecesAsync(
                    toFetch,
                    stream.Headers,
                    path,
                    done => progress.Report(Math.Clamp(offset + done - headerPieces, 0, total), total),
                    cancellationToken);
                if (download.Failed)
                {
                    return download.Error;
                }

                finishedBefore += pieces.Count;
                progress.Report(finishedBefore, total);
                inputs.Add(new CutInput(path, range.Start - pieces[0].Start));
            }
        }

        return ToolResult<IReadOnlyList<CutInput>>.Success(inputs);
    }
}
