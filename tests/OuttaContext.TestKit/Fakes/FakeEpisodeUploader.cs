using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Uploads;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Records episodes it was asked to upload. Set <see cref="Failure"/> to make it fail.</summary>
public sealed class FakeEpisodeUploader : IEpisodeUploader
{
    public Maybe<Error> Failure { get; set; }

    public List<(Episode Episode, IReadOnlyList<Clip> Clips, string? Storage)> Uploads { get; } = [];

    public Task<Result<IReadOnlyList<string>, Error>> UploadEpisodeAsync(
        Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken)
    {
        Uploads.Add((episode, clips, storage));
        return Task.FromResult(Failure.TryGetValue(out Error? failure)
            ? Result.Failure<IReadOnlyList<string>, Error>(failure)
            : Result.Success<IReadOnlyList<string>, Error>([episode.Name.Value]));
    }
}
