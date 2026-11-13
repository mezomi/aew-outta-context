using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Media.Hls;
using OuttaContext.Core.Reporting;
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
public sealed class ClipDownloader(IStreamFetcher fetcher, IMediaProcessor media, IReporter reporter) : IClipDownloader
{
    /// <summary>Extra seconds fetched before and after the clip so the cut never runs short.</summary>
    public static readonly VideoTime SegmentMargin = VideoTime.FromTrusted(1.0);

    public async Task<Result<DownloadedClip, Error>> DownloadAsync(
        ResolvedVideo video, string outputPath, ClipTimeSpan span, CancellationToken cancellationToken)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        string workDirectory = Path.Combine(folder, $".outta-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        try
        {
            Result<IReadOnlyList<CutInput>, Error> inputs = video.IsHls
                ? await FetchSegmentsAsync(video, span, workDirectory, cancellationToken)
                : video.Streams.Select(stream => new CutInput(stream.Url, span.Start, stream.Headers)).ToList();
            if (inputs.IsFailure)
            {
                return inputs.Error;
            }

            reporter.Step("Cutting the exact range");
            double length = span.Length.Seconds;
            UnitResult<Error> cut;
            using (IProgressTask progress = reporter.StartProgress("cutting", ProgressUnit.Seconds, length))
            {
                cut = await media.CutAsync(
                    inputs.Value, outputPath, span.Length, seconds => progress.Report(seconds, length), cancellationToken);
            }

            return cut.Map(() => new DownloadedClip(outputPath, ByteSize.OfFile(outputPath)));
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    private async Task<Result<IReadOnlyList<CutInput>, Error>> FetchSegmentsAsync(
        ResolvedVideo video, ClipTimeSpan span, string workDirectory, CancellationToken cancellationToken)
    {
        VideoTime start = span.Start.MinusOrZero(SegmentMargin);
        VideoTime end = span.End.Plus(SegmentMargin);

        // 1. Read each stream's playlist and pick the pieces that cover the clip.
        var plans = new List<(StreamFormat Stream, MediaPlaylist Playlist, IReadOnlyList<HlsSegment> Pieces)>();
        for (int index = 0; index < video.Streams.Count; index++)
        {
            StreamFormat stream = video.Streams[index];
            Result<string, Error> text = await fetcher.DownloadTextAsync(
                stream.Url, stream.Headers, Path.Combine(workDirectory, $"stream{index}.m3u8"), cancellationToken);
            if (text.IsFailure)
            {
                return text.Error;
            }

            Result<MediaPlaylist, Error> parsed = MediaPlaylistParser.Parse(text.Value, new Uri(stream.Url));
            if (parsed.IsFailure)
            {
                return parsed.Error;
            }

            MediaPlaylist playlist = parsed.Value;
            if (playlist.Encrypted)
            {
                return MediaErrors.Encrypted();
            }

            IReadOnlyList<HlsSegment> pieces = playlist.Covering(start, end);
            if (pieces.Count == 0)
            {
                return MediaErrors.NoPiecesInRange($"{span} (the stream is {playlist.Duration} long)");
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
                playlist.Init.Tap(init => toFetch.Add(new StreamPiece(init.Url, Maybe<VideoTime>.None, init.Range)));

                toFetch.AddRange(pieces.Select(piece => new StreamPiece(piece.Url, piece.Duration, piece.Range)));
                int headerPieces = toFetch.Count - pieces.Count;
                int offset = finishedBefore;
                string path = Path.Combine(workDirectory, $"stream{index}.mp4");
                UnitResult<Error> download = await fetcher.DownloadPiecesAsync(
                    toFetch,
                    stream.Headers,
                    path,
                    done => progress.Report(Math.Clamp(offset + done - headerPieces, 0, total), total),
                    cancellationToken);
                if (download.IsFailure)
                {
                    return download.Error;
                }

                finishedBefore += pieces.Count;
                progress.Report(finishedBefore, total);
                inputs.Add(new CutInput(path, span.Start.MinusOrZero(pieces[0].Start)));
            }
        }

        return inputs;
    }
}
