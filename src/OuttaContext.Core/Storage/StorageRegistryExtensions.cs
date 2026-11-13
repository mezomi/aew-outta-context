using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

public static class StorageRegistryExtensions
{
    /// <summary>The storage with this name, or a "not found" failure.</summary>
    public static Result<StorageConfig, Error> Get(this IStorageRegistry registry, StorageName name) =>
        registry.Find(name).ToResult(StorageErrors.NotFound(name));

    /// <summary>The storage named by the user (<paramref name="name"/>), else the default one.</summary>
    public static Result<StorageConfig, Error> Choose(this IStorageRegistry registry, string? name, Maybe<StorageName> defaultName)
    {
        if (name is not null)
        {
            return StorageName.Create(name).Bind(registry.Get);
        }

        return defaultName.TryGetValue(out StorageName? chosen) ? registry.Get(chosen) : StorageErrors.NoDefault();
    }
}
