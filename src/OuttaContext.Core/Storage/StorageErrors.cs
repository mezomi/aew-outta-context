using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>Errors for storages (`outta storage`, uploads).</summary>
public static class StorageErrors
{
    private const string AddHint = "Add one with `outta storage add NAME --provider gdrive`, then `outta storage default NAME`.";

    public static Error InvalidName(string name) =>
        new("storage.invalid_name", $"\"{name}\" is not a valid storage name.", "Use letters, numbers, - and _ (up to 32), e.g. drive.");

    public static Error UnknownProvider(string provider) =>
        new("storage.unknown_provider", $"\"{provider}\" is not a storage provider.", "Use one of: " + string.Join(", ", StorageProvider.All.Select(p => p.Id)));

    public static Error NotFound(StorageName name) =>
        new("storage.not_found", $"No storage named \"{name}\".", "`outta storage list` shows them.");

    public static Error AlreadyExists(StorageName name) =>
        new("storage.exists", $"A storage named \"{name}\" already exists.", "Pick another name, or remove it first with `outta storage remove`.");

    public static Error NoDefault() => new("storage.no_default", "No storage was named and there is no default storage.", AddHint);

    public static Error FolderRequired(StorageProvider provider) =>
        new("storage.folder_required", $"{provider.Label} needs --folder.", "e.g. --folder \"D:\\AEW backup\" (local) or --folder \"my-bucket/aew\" (b2).");

    public static Error AccountRequired() =>
        new("storage.account_required", "Backblaze B2 needs --account (your key ID).", "Find it in the Backblaze console under App Keys.");

    public static Error KeychainUnavailable(string reason) =>
        new("storage.keychain", $"There is no keychain to keep the login in: {reason}", "Use a local storage, or run outta on Windows.");
}
