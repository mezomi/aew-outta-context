using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;

namespace OuttaContext.TestKit.Fakes;

/// <summary>
/// Backends that only record what they were asked to do. A storage whose folder is "gone" fails
/// its test and uploads, like a disconnected drive.
/// </summary>
public sealed class FakeStorageBackends : IStorageBackendFactory
{
    public const string BrokenFolder = "gone";

    public List<(StorageName Storage, string Subfolder, string FileName)> Uploads { get; } = [];

    public List<StorageName> Tested { get; } = [];

    public List<StorageName> Forgotten { get; } = [];

    public IStorageBackend Create(StorageConfig config) => new Backend(config, this);

    private sealed class Backend(StorageConfig config, FakeStorageBackends owner) : IStorageBackend
    {
        public StorageConfig Config => config;

        private bool Broken => config.Folder == BrokenFolder;

        public Task<Result<UploadedFile, Error>> UploadAsync(
            string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken)
        {
            if (Broken)
            {
                return Task.FromResult(Result.Failure<UploadedFile, Error>(Gone()));
            }

            owner.Uploads.Add((config.Name, subfolder, Path.GetFileName(localPath)));
            onProgressBytes(1, 1);
            return Task.FromResult(Result.Success<UploadedFile, Error>(new UploadedFile($"{config.Name}:{subfolder}/{Path.GetFileName(localPath)}")));
        }

        public Task<UnitResult<Error>> TestAsync(CancellationToken cancellationToken)
        {
            owner.Tested.Add(config.Name);
            return Task.FromResult(Broken ? UnitResult.Failure(Gone()) : UnitResult.Success<Error>());
        }

        public Task<UnitResult<Error>> ForgetAsync(CancellationToken cancellationToken)
        {
            owner.Forgotten.Add(config.Name);
            return Task.FromResult(UnitResult.Success<Error>());
        }

        private Error Gone() => new("local.missing", $"Folder {config.Folder} does not exist or is not connected.", "Connect the drive.");
    }
}
