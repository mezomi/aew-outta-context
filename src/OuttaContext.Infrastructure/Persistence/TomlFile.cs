using OuttaContext.Core.Common;
using OuttaContext.Core.Settings;
using Tomlyn;
using Tomlyn.Model;

namespace OuttaContext.Infrastructure.Persistence;

/// <summary>Reads and writes TOML files. The only class that uses the Tomlyn library.</summary>
public static class TomlFile
{
    /// <summary>The file's contents, an empty table if it doesn't exist, or why it can't be read.</summary>
    public static Result<TomlTable, Error> Read(string path)
    {
        if (!File.Exists(path))
        {
            return new TomlTable();
        }

        try
        {
            return TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path)) ?? [];
        }
        catch (TomlException exception)
        {
            return SettingsErrors.UnreadableFile(path, $"not valid TOML ({exception.Message})");
        }
    }

    /// <summary>
    /// For files the app can't work without: a broken file is not something a caller can handle,
    /// so it throws (the CLI still prints it as an error with a hint).
    /// </summary>
    public static TomlTable ReadOrThrow(string path) => Read(path).ValueOrThrow();

    public static void Write(string path, TomlTable table)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, TomlSerializer.Serialize(table));
    }

    /// <summary>A sub-table, or an empty one.</summary>
    public static TomlTable Table(TomlTable parent, string key) =>
        parent.TryGetValue(key, out object? value) && value is TomlTable table ? table : [];

    /// <summary>A text value, or none when missing, empty or not text.</summary>
    public static Maybe<string> Text(TomlTable table, string key) =>
        table.TryGetValue(key, out object? value) && value is string { Length: > 0 } text ? text : Maybe<string>.None;
}
