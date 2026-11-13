using OuttaContext.Core.Common;
using OuttaContext.Core.Media;
using OuttaContext.Core.Time;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Pretends to be ffmpeg: writes small files and reports durations from <see cref="Durations"/>.</summary>
public sealed class FakeMediaProcessor : IMediaProcessor
{
    public bool IsAvailable { get; set; } = true;

    /// <summary>Probe results by file name; anything else is <see cref="DefaultDuration"/> long.</summary>
    public Dictionary<string, double> Durations { get; } = new(StringComparer.Ordinal);

    public double DefaultDuration { get; set; } = 20;

    /// <summary>Set to make every normalize fail.</summary>
    public Maybe<Error> NormalizeFailure { get; set; }

    public List<(string Source, string Destination)> Normalized { get; } = [];

    public List<(IReadOnlyList<string> Paths, string Destination)> Concats { get; } = [];

    public List<(IReadOnlyList<CutInput> Inputs, string Destination, VideoTime Length)> Cuts { get; } = [];

    public Task<Maybe<string>> GetVersionAsync(CancellationToken cancellationToken) =>
        Task.FromResult(IsAvailable ? Maybe.From("ffmpeg version 7.1 Copyright") : Maybe<string>.None);

    public Task<Result<ProbeResult, Error>> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        string name = Path.GetFileName(path);
        double duration = Durations.TryGetValue(name, out double known)
            ? known
            : Concats.Count > 0 && Concats[^1].Destination == path
                ? Concats[^1].Paths.Count * DefaultDuration
                : DefaultDuration;
        return Task.FromResult(Result.Success<ProbeResult, Error>(new ProbeResult(VideoTime.FromTrusted(duration), 1920, 1080, 30, true)));
    }

    public Task<UnitResult<Error>> NormalizeAsync(string source, string destination, NormalizeTarget target, CancellationToken cancellationToken)
    {
        Normalized.Add((source, destination));
        return NormalizeFailure.TryGetValue(out Error? failure) ? Task.FromResult(UnitResult.Failure(failure)) : Write(destination);
    }

    public Task<UnitResult<Error>> ConcatAsync(IReadOnlyList<string> paths, string destination, string workDirectory, CancellationToken cancellationToken)
    {
        Concats.Add((paths, destination));
        return Write(destination);
    }

    public Task<UnitResult<Error>> CutAsync(
        IReadOnlyList<CutInput> inputs, string destination, VideoTime length, Action<double> onProgressSeconds, CancellationToken cancellationToken)
    {
        Cuts.Add((inputs, destination, length));
        onProgressSeconds(length.Seconds / 2);
        onProgressSeconds(length.Seconds);
        return Write(destination);
    }

    private static Task<UnitResult<Error>> Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, [1, 2, 3]);
        return Task.FromResult(UnitResult.Success<Error>());
    }
}
