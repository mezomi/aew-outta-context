using System.Text.RegularExpressions;
using OuttaContext.Core.Errors;
using OuttaContext.Core.Results;

namespace OuttaContext.Core.Storage;

/// <summary>A configured upload destination (storage.toml). Never holds secrets.</summary>
/// <param name="Folder">Remote folder, or the destination path for a local storage.</param>
/// <param name="Options">Non-secret extras, e.g. the Backblaze key ID.</param>
public sealed record StorageConfig(string Name, string Provider, string Folder, IReadOnlyDictionary<string, string> Options)
{
    public StorageConfig(string name, string provider, string folder)
        : this(name, provider, folder, new Dictionary<string, string>())
    {
    }
}

/// <summary>The kinds of storage `outta storage add --provider` accepts.</summary>
public static partial class StorageProviders
{
    public const string GoogleDrive = "gdrive";
    public const string OneDrive = "onedrive";
    public const string Dropbox = "dropbox";
    public const string Backblaze = "b2";
    public const string LocalFolder = "local";

    public static readonly IReadOnlyList<string> All = [GoogleDrive, OneDrive, Dropbox, Backblaze, LocalFolder];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [GoogleDrive] = "Google Drive",
        [OneDrive] = "OneDrive",
        [Dropbox] = "Dropbox",
        [Backblaze] = "Backblaze B2",
        [LocalFolder] = "Local folder",
    };

    public static bool UsesRclone(string provider) => provider != LocalFolder;

    public static string ValidateName(string name) =>
        StorageNameRegex().IsMatch(name)
            ? name
            : throw new SettingsException(
                $"\"{name}\" is not a valid storage name.", "Use letters, numbers, - and _ (up to 32), e.g. drive.");

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$")]
    private static partial Regex StorageNameRegex();
}

/// <summary>Somewhere files are uploaded to.</summary>
public interface IStorageBackend
{
    StorageConfig Config { get; }

    /// <summary>Uploads into &lt;folder&gt;/&lt;subfolder&gt;/ and returns where the file went.</summary>
    Task<ToolResult<string>> UploadAsync(
        string localPath, string subfolder, Action<double, double?> onProgressBytes, CancellationToken cancellationToken);

    /// <summary>Checks the storage works (writes and deletes a tiny file).</summary>
    Task<ToolResult<Done>> TestAsync(CancellationToken cancellationToken);

    /// <summary>Removes logins and stored keys when the storage is removed.</summary>
    Task<ToolResult<Done>> ForgetAsync(CancellationToken cancellationToken);
}

/// <summary>A storage that needs a one-time sign-in (browser login or an API key).</summary>
public interface ISignInStorage : IStorageBackend
{
    /// <param name="askSecret">Asks the user for a secret (hidden input), e.g. a Backblaze key.</param>
    Task<ToolResult<Done>> SignInAsync(Func<string, string> askSecret, CancellationToken cancellationToken);
}

/// <summary>The configured storages and the backend for each.</summary>
public interface IStorageRegistry
{
    IReadOnlyList<StorageConfig> List();

    StorageConfig Get(string name);

    void Add(StorageConfig config);

    StorageConfig Remove(string name);

    IStorageBackend Backend(StorageConfig config);
}

/// <summary>The system keychain (Windows Credential Manager). The only place secrets are kept.</summary>
public interface ICredentialStore
{
    /// <summary>Whether a keychain is usable here, and which one.</summary>
    (bool Available, string Name) Describe();

    string? Get(string key);

    void Set(string key, string secret);

    void Delete(string key);
}
