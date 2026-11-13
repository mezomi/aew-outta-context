using OuttaContext.Core.Storage;
using OuttaContext.TestKit.Assertions;

namespace OuttaContext.Core.Tests.Storage;

public sealed class StorageNameTests
{
    [Theory]
    [InlineData("drive")]
    [InlineData("backup_2")]
    [InlineData("my-b2")]
    public void Create_ValidName_Succeeds(string name)
    {
        StorageName.Create(name).ShouldSucceed().Value.ShouldBe(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("my drive")]
    [InlineData("-lead")]
    [InlineData("waytoolongnamewaytoolongnamewaytoolong")]
    public void Create_InvalidName_Fails(string name)
    {
        StorageName.Create(name).ShouldFailWith("storage.invalid_name");
    }
}
