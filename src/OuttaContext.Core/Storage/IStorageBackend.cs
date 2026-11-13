using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>Somewhere files are uploaded to.</summary>
public interface IStorageBackend
{
    StorageConfig Config { get; }

    /// <summary>Uploads into &lt;folder&gt;/&lt;subfolder&gt;/.</summary>
    Task<Result<UploadedFile, Error>> UploadAsync(
        string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken);

    /// <summary>Checks the storage works (writes and deletes a tiny file).</summary>
    Task<UnitResult<Error>> TestAsync(CancellationToken cancellationToken);

    /// <summary>Removes logins and stored keys when the storage is removed.</summary>
    Task<UnitResult<Error>> ForgetAsync(CancellationToken cancellationToken);
}
