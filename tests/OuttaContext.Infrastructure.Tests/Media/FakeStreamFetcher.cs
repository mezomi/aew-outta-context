using OuttaContext.Core.Common;
using OuttaContext.Core.Sources;
using OuttaContext.Infrastructure.YtDlp;

namespace OuttaContext.Infrastructure.Tests.Media;

/// <summary>Serves playlists from a dictionary and "downloads" pieces by writing a small file.</summary>
internal sealed class FakeStreamFetcher : IStreamFetcher
{
    public Dictionary<string, string> Playlists { get; } = new(StringComparer.Ordinal);

    public Maybe<Error> PieceFailure { get; set; }

    public List<(IReadOnlyList<StreamPiece> Pieces, RequestHeaders Headers)> PieceDownloads { get; } = [];

    public Task<Result<string, Error>> DownloadTextAsync(string url, RequestHeaders headers, string workFile, CancellationToken cancellationToken) =>
        Task.FromResult(Playlists.TryGetValue(url, out string? text)
            ? Result.Success<string, Error>(text)
            : Result.Failure<string, Error>(new Error("yt-dlp.unavailable", "404", "hint")));

    public Task<UnitResult<Error>> DownloadPiecesAsync(
        IReadOnlyList<StreamPiece> pieces, RequestHeaders headers, string outputPath, Action<int> onPieceDone, CancellationToken cancellationToken)
    {
        PieceDownloads.Add((pieces, headers));
        if (PieceFailure.TryGetValue(out Error? failure))
        {
            return Task.FromResult(UnitResult.Failure(failure));
        }

        for (int index = 1; index <= pieces.Count; index++)
        {
            onPieceDone(index);
        }

        File.WriteAllText(outputPath, "pieces");
        return Task.FromResult(UnitResult.Success<Error>());
    }
}
