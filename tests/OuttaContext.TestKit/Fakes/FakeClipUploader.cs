using OuttaContext.Core.Common;
using OuttaContext.Core.Library;
using OuttaContext.Core.Uploads;

namespace OuttaContext.TestKit.Fakes;

/// <summary>Records clips it was asked to upload. Set <see cref="Failure"/> to make it fail.</summary>
public sealed class FakeClipUploader : IClipUploader
{
    public Maybe<Error> Failure { get; set; }

    public List<(Clip Clip, string? Storage)> Uploads { get; } = [];

    public Task<Result<string, Error>> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken)
    {
        Uploads.Add((clip, storage));
        return Task.FromResult(Failure.TryGetValue(out Error? failure)
            ? Result.Failure<string, Error>(failure)
            : Result.Success<string, Error>($"{storage ?? "default"}:{Path.GetFileName(clip.FilePath)}"));
    }
}
