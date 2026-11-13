using OuttaContext.Core.Common;

namespace OuttaContext.Core.Storage;

/// <summary>The kinds of storage `outta storage add --provider` accepts. <see cref="EnumValueObject{T}.Id"/> is the name typed and stored.</summary>
public sealed class StorageProvider : EnumValueObject<StorageProvider>
{
    public static readonly StorageProvider GoogleDrive = new("gdrive", "Google Drive", usesRclone: true, signsInWithBrowser: true);
    public static readonly StorageProvider OneDrive = new("onedrive", "OneDrive", usesRclone: true, signsInWithBrowser: true);
    public static readonly StorageProvider Dropbox = new("dropbox", "Dropbox", usesRclone: true, signsInWithBrowser: true);
    public static readonly StorageProvider Backblaze = new("b2", "Backblaze B2", usesRclone: true, signsInWithBrowser: false);
    public static readonly StorageProvider LocalFolder = new("local", "Local folder", usesRclone: false, signsInWithBrowser: false);

    private StorageProvider(string id, string label, bool usesRclone, bool signsInWithBrowser)
        : base(id)
    {
        Label = label;
        UsesRclone = usesRclone;
        SignsInWithBrowser = signsInWithBrowser;
    }

    public string Label { get; }

    public bool UsesRclone { get; }

    /// <summary>Google Drive, OneDrive and Dropbox open the browser to sign in.</summary>
    public bool SignsInWithBrowser { get; }

    public static Result<StorageProvider, Error> Parse(string text) =>
        FromId(text.Trim().ToLowerInvariant()).ToResult(StorageErrors.UnknownProvider(text));

    public override string ToString() => Id;
}
