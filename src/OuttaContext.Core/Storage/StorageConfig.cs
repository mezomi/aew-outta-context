namespace OuttaContext.Core.Storage;

/// <summary>A configured upload destination (storage.toml). Never holds secrets.</summary>
/// <param name="Folder">Remote folder, or the destination path for a local storage.</param>
/// <param name="Options">Non-secret extras, e.g. the Backblaze key ID ("account").</param>
public sealed record StorageConfig(StorageName Name, StorageProvider Provider, string Folder, IReadOnlyDictionary<string, string> Options)
{
    public const string AccountOption = "account";

    public StorageConfig(StorageName name, StorageProvider provider, string folder)
        : this(name, provider, folder, new Dictionary<string, string>())
    {
    }

    public Maybe<string> Option(string key) => Options.TryGetValue(key, out string? value) ? value : Maybe<string>.None;
}
