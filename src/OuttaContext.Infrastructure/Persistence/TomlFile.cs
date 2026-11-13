using OuttaContext.Core.Errors;
using Tomlyn;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>Reads and writes TOML files. The only class that uses the Tomlyn library.</summary>
public static class TomlFile
{
    /// <summary>The file's contents, or an empty table if it doesn't exist.</summary>
    public static TomlTable Read(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path)) ?? [];
        }
        catch (TomlException exception)
        {
            throw new SettingsException($"{path} is not valid TOML: {exception.Message}");
        }
    }

    public static void Write(string path, TomlTable table)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, TomlSerializer.Serialize(table));
    }

    /// <summary>A sub-table, or an empty one.</summary>
    public static TomlTable Table(TomlTable parent, string key) =>
        parent.TryGetValue(key, out object? value) && value is TomlTable table ? table : [];
}
