using OuttaContext.Core.Common;
using OuttaContext.Core.Storage;

namespace OuttaContext.TestKit.Fakes;

/// <summary>storage.toml kept in memory.</summary>
public sealed class InMemoryStorageRegistry : IStorageRegistry
{
    public List<StorageConfig> Configs { get; } = [];

    public IReadOnlyList<StorageConfig> List() => Configs;

    public Maybe<StorageConfig> Find(StorageName name) => Configs.TryFirst(config => config.Name == name);

    public UnitResult<Error> Add(StorageConfig config)
    {
        if (Find(config.Name).HasValue)
        {
            return StorageErrors.AlreadyExists(config.Name);
        }

        Configs.Add(config);
        return UnitResult.Success<Error>();
    }

    public Result<StorageConfig, Error> Remove(StorageName name) =>
        this.Get(name).Tap(config => Configs.Remove(config));
}
