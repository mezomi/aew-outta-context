using OuttaContext.Core.Storage;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>A local folder is copied to directly; everything else goes through rclone.</summary>
public sealed class StorageBackendFactory(Rclone rclone) : IStorageBackendFactory
{
    public IStorageBackend Create(StorageConfig config) =>
        config.Provider.UsesRclone ? new RcloneBackend(config, rclone) : new LocalFolderBackend(config);
}
