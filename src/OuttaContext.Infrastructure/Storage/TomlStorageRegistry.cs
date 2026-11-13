using OuttaContext.Core.Common;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Settings;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Persistence;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>storage.toml: names, providers, folders and non-secret options. Never secrets.</summary>
public sealed class TomlStorageRegistry(AppPaths paths) : IStorageRegistry
{
    private string Location => paths.StorageFile;

    public IReadOnlyList<StorageConfig> List()
    {
        TomlTable storage = TomlFile.Table(TomlFile.ReadOrThrow(Location), "storage");
        return storage
            .Where(entry => entry.Value is TomlTable)
            .Select(entry => Read(entry.Key, (TomlTable)entry.Value))
            .ToList();
    }

    public Maybe<StorageConfig> Find(StorageName name) => List().TryFirst(config => config.Name == name);

    public UnitResult<Error> Add(StorageConfig config)
    {
        List<StorageConfig> all = [.. List()];
        if (all.Any(existing => existing.Name == config.Name))
        {
            return StorageErrors.AlreadyExists(config.Name);
        }

        all.Add(config);
        Save(all);
        return UnitResult.Success<Error>();
    }

    public Result<StorageConfig, Error> Remove(StorageName name) =>
        this.Get(name).Tap(_ => Save(List().Where(existing => existing.Name != name).ToList()));

    /// <summary>One [storage.NAME] table. A broken entry means the file was edited by hand, so it throws.</summary>
    private StorageConfig Read(string key, TomlTable item)
    {
        Error Broken(Error error) => SettingsErrors.UnreadableFile(Location, $"storage \"{key}\": {error.Message}");

        StorageName name = StorageName.Create(key).ValueOrThrow(error => Broken(error).ToException());
        StorageProvider provider = StorageProvider.Parse(TomlFile.Text(item, "provider").GetValueOrDefault(string.Empty))
            .ValueOrThrow(error => Broken(error).ToException());
        var options = TomlFile.Table(item, "options").ToDictionary(
            option => option.Key, option => option.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);
        return new StorageConfig(name, provider, TomlFile.Text(item, "folder").GetValueOrDefault(string.Empty), options);
    }

    private void Save(IEnumerable<StorageConfig> configs)
    {
        var storage = new TomlTable();
        foreach (StorageConfig config in configs.OrderBy(c => c.Name.Value, StringComparer.Ordinal))
        {
            var item = new TomlTable { ["provider"] = config.Provider.Id, ["folder"] = config.Folder };
            if (config.Options.Count > 0)
            {
                var options = new TomlTable();
                foreach ((string key, string value) in config.Options)
                {
                    options[key] = value;
                }

                item["options"] = options;
            }

            storage[config.Name.Value] = item;
        }

        TomlFile.Write(Location, new TomlTable { ["storage"] = storage });
    }
}
