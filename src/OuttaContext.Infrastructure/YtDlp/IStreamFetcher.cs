using OuttaContext.Core.Media.Hls;
using OuttaContext.Core.Results;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>One piece of a stream to download (an HLS segment or its header piece).</summary>
public sealed record StreamPiece(string Url, double? Duration = null, ByteRange? Range = null);

/// <summary>
/// Downloads files and stream pieces through yt-dlp, posing as a browser when needed.
/// Sites like Dailymotion refuse requests that don't look like Chrome/Firefox, which plain
/// .NET HttpClient and ffmpeg can't imitate.
/// </summary>
public interface IStreamFetcher
{
    /// <summary>Downloads an .m3u8 playlist and returns its text. <paramref name="workFile"/> must end in .m3u8.</summary>
    Task<ToolResult<string>> DownloadTextAsync(
        string url, IReadOnlyDictionary<string, string> headers, string workFile, CancellationToken cancellationToken);

    /// <summary>Downloads the pieces in order and joins them into <paramref name="outputPath"/>.</summary>
    Task<ToolResult<Done>> DownloadPiecesAsync(
        IReadOnlyList<StreamPiece> pieces,
        IReadOnlyDictionary<string, string> headers,
        string outputPath,
        Action<int> onPieceDone,
        CancellationToken cancellationToken);
}
