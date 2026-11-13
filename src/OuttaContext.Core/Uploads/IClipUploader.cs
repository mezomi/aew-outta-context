using OuttaContext.Core.Common;
using OuttaContext.Core.Library;

namespace OuttaContext.Core.Uploads;

/// <summary>Uploads one clip (used by `outta clip --upload`).</summary>
public interface IClipUploader
{
    /// <returns>Where the clip went.</returns>
    Task<Result<string, Error>> UploadClipAsync(Clip clip, string? storage, CancellationToken cancellationToken);
}
