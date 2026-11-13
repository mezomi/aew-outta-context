using OuttaContext.Core.Common;
using OuttaContext.Core.Paths;
using OuttaContext.Core.Storage;
using OuttaContext.Infrastructure.Storage;
using OuttaContext.TestKit.Assertions;
using OuttaContext.TestKit.Builders;

namespace OuttaContext.Infrastructure.Tests.Storage;

public sealed class TomlStorageRegistryTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Add_Storages_AreSavedWithOptionsAndNoSecrets()
    {
        Registry().Add(new StorageConfig(Values.Storage("drive"), StorageProvider.GoogleDrive, "AEW Outta Context")).ShouldSucceed();
        Registry().Add(new StorageConfig(Values.Storage("b2"), StorageProvider.Backblaze, "bucket", new Dictionary<string, string> { ["account"] = "acc" }))
            .ShouldSucceed();

        Registry().Get(Values.Storage("b2")).ShouldSucceed().Option("account").ShouldHaveValue().ShouldBe("acc");
        Registry().List().Select(c => c.Name.Value).ShouldBe(["b2", "drive"]);
    }

    [Fact]
    public void Add_SameNameTwice_IsRefused()
    {
        Registry().Add(new StorageConfig(Values.Storage("drive"), StorageProvider.GoogleDrive, "x")).ShouldSucceed();

        Registry().Add(new StorageConfig(Values.Storage("drive"), StorageProvider.GoogleDrive, "y")).ShouldFailWith("storage.exists");
    }

    [Fact]
    public void Remove_Storage_IsGone()
    {
        Registry().Add(new StorageConfig(Values.Storage("nas"), StorageProvider.LocalFolder, "/mnt/nas")).ShouldSucceed();

        Registry().Remove(Values.Storage("nas")).ShouldSucceed();

        Registry().Find(Values.Storage("nas")).HasNoValue.ShouldBeTrue();
        Registry().Remove(Values.Storage("nas")).ShouldFailWith("storage.not_found");
    }

    [Fact]
    public void List_FileWrittenByThePythonVersion_IsRead()
    {
        _temp.Write("storage.toml", "[storage.drive]\nprovider = \"gdrive\"\nfolder = \"AEW Outta Context\"\n\n[storage.b2]\nprovider = \"b2\"\nfolder = \"bucket\"\n\n[storage.b2.options]\naccount = \"acc\"\n");

        Registry().Get(Values.Storage("drive")).ShouldSucceed().Provider.ShouldBe(StorageProvider.GoogleDrive);
        Registry().Get(Values.Storage("b2")).ShouldSucceed().Option("account").ShouldHaveValue().ShouldBe("acc");
    }

    [Fact]
    public void List_UnknownProviderInFile_ThrowsNamingTheStorage()
    {
        _temp.Write("storage.toml", "[storage.drive]\nprovider = \"icloud\"\n");

        Should.Throw<ErrorException>(() => Registry().List()).Error.Message.ShouldContain("storage \"drive\"");
    }

    private TomlStorageRegistry Registry() => new(new AppPaths(_temp.Path));
}
