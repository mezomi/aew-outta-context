using OuttaContext.Core.Errors;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Persistence;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Storage;

/// <summary>storage.toml (names, providers, folders; never secrets) and the backend for each storage.</summary>
public sealed class TomlStorageRegistry(string path, Func<StorageConfig, IStorageBackend> backendFactory) : IStorageRegistry
{
    public IReadOnlyList<StorageConfig> List()
    {
        TomlTable storage = TomlFile.Table(TomlFile.Read(path), "storage");
        return storage
            .Where(entry => entry.Value is TomlTable)
            .Select(entry =>
            {
                var item = (TomlTable)entry.Value;
                var options = TomlFile.Table(item, "options").ToDictionary(
                    option => option.Key, option => option.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);
                return new StorageConfig(entry.Key, Text(item, "provider"), Text(item, "folder"), options);
            })
            .ToList();
    }

    private static string Text(TomlTable table, string key) =>
        table.TryGetValue(key, out object? value) ? value?.ToString() ?? string.Empty : string.Empty;

    public StorageConfig Get(string name) =>
        List().FirstOrDefault(config => config.Name == name)
        ?? throw new StorageNotConfiguredException($"No storage named \"{name}\". See storages with `outta storage list`.");

    public void Add(StorageConfig config)
    {
        List<StorageConfig> all = [.. List()];
        if (all.Any(existing => existing.Name == config.Name))
        {
            throw new AlreadyExistsException(
                $"A storage named \"{config.Name}\" already exists.", "Pick another name, or remove the old one with `outta storage remove NAME`.");
        }

        all.Add(config);
        Save(all);
    }

    public StorageConfig Remove(string name)
    {
        StorageConfig config = Get(name);
        Save(List().Where(existing => existing.Name != name).ToList());
        return config;
    }

    public IStorageBackend Backend(StorageConfig config) => backendFactory(config);

    private void Save(IEnumerable<StorageConfig> configs)
    {
        var storage = new TomlTable();
        foreach (StorageConfig config in configs.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            var item = new TomlTable { ["provider"] = config.Provider, ["folder"] = config.Folder };
            if (config.Options.Count > 0)
            {
                var options = new TomlTable();
                foreach ((string key, string value) in config.Options)
                {
                    options[key] = value;
                }

                item["options"] = options;
            }

            storage[config.Name] = item;
        }

        TomlFile.Write(path, new TomlTable { ["storage"] = storage });
    }
}
