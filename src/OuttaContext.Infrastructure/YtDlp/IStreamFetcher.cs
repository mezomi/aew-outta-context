using OuttaContext.Core.Common;
using OuttaContext.Core.Sources;

namespace OuttaContext.Infrastructure.YtDlp;

/// <summary>
/// Downloads files and stream pieces through yt-dlp, posing as a browser when needed.
/// Sites like Dailymotion refuse requests that don't look like Chrome/Firefox, which plain
/// .NET HttpClient and ffmpeg can't imitate.
/// </summary>
public interface IStreamFetcher
{
    /// <summary>Downloads an .m3u8 playlist and returns its text. <paramref name="workFile"/> must end in .m3u8.</summary>
    Task<Result<string, Error>> DownloadTextAsync(
        string url, RequestHeaders headers, string workFile, CancellationToken cancellationToken);

    /// <summary>Downloads the pieces in order and joins them into <paramref name="outputPath"/>.</summary>
    /// <param name="onPieceDone">Called with the number of the piece just finished (1-based).</param>
    Task<UnitResult<Error>> DownloadPiecesAsync(
        IReadOnlyList<StreamPiece> pieces,
        RequestHeaders headers,
        string outputPath,
        Action<int> onPieceDone,
        CancellationToken cancellationToken);
}
