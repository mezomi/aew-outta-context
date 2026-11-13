using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>The configured storages (storage.toml).</summary>
public interface IStorageRegistry
{
    IReadOnlyList<StorageConfig> List();

    Maybe<StorageConfig> Find(StorageName name);

    /// <returns>A failure if the name is taken.</returns>
    UnitResult<Error> Add(StorageConfig config);

    /// <returns>The removed storage, or a failure if there is none by that name.</returns>
    Result<StorageConfig, Error> Remove(StorageName name);
}
