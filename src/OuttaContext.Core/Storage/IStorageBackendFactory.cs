namespace OuttaContext.Core.Storage;

/// <summary>Creates the backend (rclone or a local folder) for a configured storage.</summary>
public interface IStorageBackendFactory
{
    IStorageBackend Create(StorageConfig config);
}
