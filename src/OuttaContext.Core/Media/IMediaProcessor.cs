using OuttaContext.Core.Results;

namespace OuttaContext.Core.Media;

/// <summary>What the app needs from ffmpeg and ffprobe.</summary>
public interface IMediaProcessor
{
    bool IsAvailable { get; }

    Task<string?> GetVersionAsync(CancellationToken cancellationToken);

    Task<ToolResult<ProbeResult>> ProbeAsync(string path, CancellationToken cancellationToken);

    /// <summary>Converts a clip to the common size/frame rate/codecs used for episodes.</summary>
    Task<ToolResult<Done>> NormalizeAsync(
        string source, string destination, NormalizeTarget target, CancellationToken cancellationToken);

    /// <summary>Joins clips that were already normalized (no re-encoding).</summary>
    Task<ToolResult<Done>> ConcatAsync(
        IReadOnlyList<string> paths, string destination, string workDirectory, CancellationToken cancellationToken);

    /// <summary>Cuts <paramref name="length"/> seconds from the inputs, re-encoding for exact edges.</summary>
    Task<ToolResult<Done>> CutAsync(
        IReadOnlyList<CutInput> inputs,
        string destination,
        double length,
        Action<double> onProgressSeconds,
        CancellationToken cancellationToken);
}
