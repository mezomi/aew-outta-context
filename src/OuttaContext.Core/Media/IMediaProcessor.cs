using OuttaContext.Core.Common;
using OuttaContext.Core.Time;

namespace OuttaContext.Core.Media;

/// <summary>What the app needs from ffmpeg and ffprobe.</summary>
public interface IMediaProcessor
{
    bool IsAvailable { get; }

    /// <summary>ffmpeg's version line, or none if it can't be run.</summary>
    Task<Maybe<string>> GetVersionAsync(CancellationToken cancellationToken);

    Task<Result<ProbeResult, Error>> ProbeAsync(string path, CancellationToken cancellationToken);

    /// <summary>Converts a clip to the common size/frame rate/codecs used for episodes.</summary>
    Task<UnitResult<Error>> NormalizeAsync(
        string source, string destination, NormalizeTarget target, CancellationToken cancellationToken);

    /// <summary>Joins clips that were already normalized (no re-encoding).</summary>
    Task<UnitResult<Error>> ConcatAsync(
        IReadOnlyList<string> paths, string destination, string workDirectory, CancellationToken cancellationToken);

    /// <summary>Cuts <paramref name="length"/> from the inputs, re-encoding for exact edges.</summary>
    /// <param name="onProgressSeconds">Called with how many seconds of the clip are written so far.</param>
    Task<UnitResult<Error>> CutAsync(
        IReadOnlyList<CutInput> inputs,
        string destination,
        VideoTime length,
        Action<double> onProgressSeconds,
        CancellationToken cancellationToken);
}
