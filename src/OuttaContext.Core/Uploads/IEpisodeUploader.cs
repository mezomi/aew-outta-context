using OuttaContext.Core.Common;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Uploads;

/// <summary>Uploads a built episode and its clips (used by `outta episode build --upload`).</summary>
public interface IEpisodeUploader
{
    /// <returns>Where each file went.</returns>
    Task<Result<IReadOnlyList<string>, Error>> UploadEpisodeAsync(
        Episode episode, IReadOnlyList<Clip> clips, string? storage, CancellationToken cancellationToken);
}
