using OuttaContext.Core.Errors;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Storage;

public sealed class TomlStorageRegistryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Add_Storages_AreSavedWithOptionsAndNoSecrets()
    {
        Registry().Add(new StorageConfig("drive", StorageProviders.GoogleDrive, "AEW Outta Context"));
        Registry().Add(new StorageConfig("b2", StorageProviders.Backblaze, "bucket", new Dictionary<string, string> { ["account"] = "acc" }));

        Registry().Get("b2").Options["account"].ShouldBe("acc");
        Registry().List().Select(c => c.Name).ShouldBe(["b2", "drive"]);
    }

    [Fact]
    public void Add_SameNameTwice_IsRefused()
    {
        Registry().Add(new StorageConfig("drive", StorageProviders.GoogleDrive, "x"));

        Should.Throw<AlreadyExistsException>(() => Registry().Add(new StorageConfig("drive", StorageProviders.GoogleDrive, "y")));
    }

    [Fact]
    public void Remove_Storage_IsGone()
    {
        Registry().Add(new StorageConfig("nas", StorageProviders.LocalFolder, "/mnt/nas"));

        Registry().Remove("nas");

        Should.Throw<StorageNotConfiguredException>(() => Registry().Get("nas"));
    }

    [Fact]
    public void List_FileWrittenByThePythonVersion_IsRead()
    {
        _temp.Write("storage.toml", "[storage.drive]\nprovider = \"gdrive\"\nfolder = \"AEW Outta Context\"\n\n[storage.b2.options]\naccount = \"acc\"\n");

        Registry().Get("drive").Provider.ShouldBe("gdrive");
    }

    private TomlStorageRegistry Registry() => new(_temp.File("storage.toml"), config => new LocalFolderBackend(config));
}
